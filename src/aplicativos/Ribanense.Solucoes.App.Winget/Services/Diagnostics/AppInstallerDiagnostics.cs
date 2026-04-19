using System.Text.Json;
using Ribanense.Solucoes.App.Winget.Services;

namespace Ribanense.Solucoes.App.Winget.Services.Diagnostics;

/// <summary>
/// Inspeciona a saude do ecossistema winget / App Installer:
/// localiza o winget, pega sua versao, e verifica se os 3 pacotes AppX
/// sao visiveis via <c>Get-AppxPackage</c> (sem UAC).
/// </summary>
public sealed class AppInstallerDiagnostics : IAppInstallerDiagnostics
{
    public const string AppInstallerName = "Microsoft.DesktopAppInstaller";
    public const string VcLibsName = "Microsoft.VCLibs.140.00.UWPDesktop";
    public const string UiXamlName = "Microsoft.UI.Xaml.2.8";

    private readonly IWingetLocator _locator;
    private readonly IWingetExecutor _executor;
    private readonly IPowerShellRunner _powerShell;

    public AppInstallerDiagnostics(
        IWingetLocator locator,
        IWingetExecutor executor,
        IPowerShellRunner powerShell)
    {
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _powerShell = powerShell ?? throw new ArgumentNullException(nameof(powerShell));
    }

    public async Task<AppInstallerStatus> InspectAsync(CancellationToken ct)
    {
        WingetStatus winget = await CheckWingetAsync(ct).ConfigureAwait(false);

        PackageStatus appInstaller = await CheckPackageAsync(AppInstallerName, ct).ConfigureAwait(false);
        PackageStatus vcLibs = await CheckPackageAsync(VcLibsName, ct).ConfigureAwait(false);
        PackageStatus uiXaml = await CheckPackageAsync(UiXamlName, ct).ConfigureAwait(false);

        return new AppInstallerStatus(winget, appInstaller, vcLibs, uiXaml);
    }

    private async Task<WingetStatus> CheckWingetAsync(CancellationToken ct)
    {
        string? path = _locator.TryLocate();
        if (path is null)
        {
            return new WingetStatus(Found: false, Path: null, Version: null, Error: "winget.exe nao encontrado.");
        }

        try
        {
            var result = await _executor.RunAsync(new[] { "--version" }, ct: ct).ConfigureAwait(false);
            string version = (result.Stdout ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(version))
            {
                version = (result.Stderr ?? string.Empty).Trim();
            }
            return new WingetStatus(Found: true, Path: path, Version: version, Error: null);
        }
        catch (Exception ex)
        {
            return new WingetStatus(Found: true, Path: path, Version: null, Error: ex.Message);
        }
    }

    private async Task<PackageStatus> CheckPackageAsync(string packageName, CancellationToken ct)
    {
        // ConvertTo-Json com -Depth 2 eh suficiente para Name/Version/PackageFullName.
        // Verbose/Progress silenciados para garantir stdout limpo.
        string cmd =
            "$ErrorActionPreference='SilentlyContinue'; " +
            "$pkg = Get-AppxPackage -Name '" + packageName.Replace("'", "''") + "' " +
            "| Select-Object -First 1 Name,Version,PackageFullName; " +
            "if ($pkg) { $pkg | ConvertTo-Json -Compress } else { '{}' }";

        try
        {
            var result = await _powerShell.RunAsync(cmd, ct).ConfigureAwait(false);
            return ParsePackageJson(result.Stdout);
        }
        catch
        {
            return new PackageStatus(Installed: false, Version: null, FullName: null);
        }
    }

    internal static PackageStatus ParsePackageJson(string stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout)) return new PackageStatus(false, null, null);
        string trimmed = stdout.Trim();
        if (trimmed == "{}") return new PackageStatus(false, null, null);

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;
            string? version = root.TryGetProperty("Version", out var v) ? v.GetString() : null;
            string? full = root.TryGetProperty("PackageFullName", out var f) ? f.GetString() : null;
            string? name = root.TryGetProperty("Name", out var n) ? n.GetString() : null;

            bool installed = !string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(full);
            return new PackageStatus(Installed: installed, Version: version, FullName: full);
        }
        catch
        {
            return new PackageStatus(false, null, null);
        }
    }
}
