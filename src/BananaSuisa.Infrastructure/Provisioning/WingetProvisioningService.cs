using System.Linq;
using System.Net.Http;
using System.Text.Json;
using BananaSuisa.Core.Provisioning;
using BananaSuisa.Services.Abstractions;
using BananaSuisa.Shared.Results;

namespace BananaSuisa.Infrastructure.Provisioning;

public sealed class WingetProvisioningService : IWingetProvisioningService
{
    private const string GitHubLatestReleaseApi = "https://api.github.com/repos/microsoft/winget-cli/releases/latest";
    private readonly IWingetLocator _locator;
    private readonly HttpClient _http;

    public WingetProvisioningService(IWingetLocator locator, HttpClient http)
    {
        _locator = locator;
        _http = http;
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("BananaSuisa-WingetProvisioning/1.0");
        }
    }

    public async Task<WingetProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new WingetProbeResult(false, null, null, null, -1, false, "Disponivel apenas no Windows.");
        }

        string? path = _locator.TryLocate();
        if (string.IsNullOrWhiteSpace(path))
        {
            return new WingetProbeResult(false, null, null, null, -1, false, "winget.exe nao encontrado no PATH nem em LocalAppData\\Microsoft\\WindowsApps.");
        }

        ProcessRunResult versionRun = await RunWingetAsync(path, "--version", cancellationToken).ConfigureAwait(false);
        ProcessRunResult sourceRun = await RunWingetAsync(path, "source list", cancellationToken).ConfigureAwait(false);

        bool versionOk = versionRun.ExitCode == 0 && !string.IsNullOrWhiteSpace(versionRun.StandardOutput);
        bool sourceOk = sourceRun.ExitCode == 0;
        bool healthy = versionOk && sourceOk;

        string summary = healthy
            ? $"winget OK em {path}. Versao: {versionRun.StandardOutput.Trim()}"
            : $"Falha na integridade: --version exit={versionRun.ExitCode}, source list exit={sourceRun.ExitCode}. {versionRun.StandardError}{sourceRun.StandardError}";

        return new WingetProbeResult(
            true,
            path,
            versionRun.StandardOutput.Trim(),
            sourceRun.StandardOutput.Trim(),
            sourceRun.ExitCode,
            healthy,
            summary);
    }

    public async Task<OperationResult> InstallLatestFromGitHubReleaseAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return OperationResult.Failure("Instalacao suportada apenas no Windows.");
        }

        try
        {
            string bundlePath = await DownloadLatestMsixBundleAsync(cancellationToken).ConfigureAwait(false);
            return await AddAppxPackageAsync(bundlePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return OperationResult.Failure($"Erro ao baixar ou instalar o pacote: {ex.Message}");
        }
    }

    public async Task<OperationResult> ReinstallAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return OperationResult.Failure("Reinstalacao suportada apenas no Windows.");
        }

        await RemoveDesktopAppInstallerAsync(cancellationToken).ConfigureAwait(false);
        return await InstallLatestFromGitHubReleaseAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<ProcessRunResult> RunWingetAsync(string wingetPath, string arguments, CancellationToken cancellationToken)
    {
        return await ProcessRunner.RunAsync(wingetPath, arguments, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> DownloadLatestMsixBundleAsync(CancellationToken cancellationToken)
    {
        using HttpRequestMessage req = new(HttpMethod.Get, GitHubLatestReleaseApi);
        req.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");

        using HttpResponseMessage response = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        JsonElement root = doc.RootElement;
        if (!root.TryGetProperty("assets", out JsonElement assets))
        {
            throw new InvalidOperationException("Resposta do GitHub sem lista de assets.");
        }

        string? downloadUrl = null;
        foreach (JsonElement asset in assets.EnumerateArray())
        {
            if (!asset.TryGetProperty("name", out JsonElement nameEl))
            {
                continue;
            }

            string? name = nameEl.GetString();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (name.Contains("DesktopAppInstaller", StringComparison.OrdinalIgnoreCase) &&
                name.EndsWith(".msixbundle", StringComparison.OrdinalIgnoreCase))
            {
                if (asset.TryGetProperty("browser_download_url", out JsonElement urlEl))
                {
                    downloadUrl = urlEl.GetString();
                }

                break;
            }
        }

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            throw new InvalidOperationException("Nao foi encontrado o asset Microsoft.DesktopAppInstaller*.msixbundle no ultimo release.");
        }

        string dir = Path.Combine(Path.GetTempPath(), "BananaSuisa", "winget");
        Directory.CreateDirectory(dir);
        string filePath = Path.Combine(dir, "Microsoft.DesktopAppInstaller.msixbundle");

        using HttpRequestMessage dl = new(HttpMethod.Get, downloadUrl);
        using HttpResponseMessage fileResp = await _http.SendAsync(dl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        fileResp.EnsureSuccessStatusCode();

        await using (Stream dlStream = await fileResp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        await using (FileStream fs = new(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await dlStream.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
        }

        return filePath;
    }

    private static async Task<OperationResult> AddAppxPackageAsync(string msixBundlePath, CancellationToken cancellationToken)
    {
        string escaped = msixBundlePath.Replace("'", "''", StringComparison.Ordinal);
        string script = $"Add-AppxPackage -Path '{escaped}' -ErrorAction Stop";
        ProcessRunResult run = await PowerShellInvoker.RunScriptAsync(script, cancellationToken).ConfigureAwait(false);
        if (run.ExitCode != 0)
        {
            return OperationResult.Failure($"Add-AppxPackage falhou (exit {run.ExitCode}). {run.StandardOutput}{run.StandardError}");
        }

        return OperationResult.Success($"Pacote instalado a partir de: {msixBundlePath}");
    }

    private static async Task<OperationResult> RemoveDesktopAppInstallerAsync(CancellationToken cancellationToken)
    {
        const string script = "Get-AppxPackage *Microsoft.DesktopAppInstaller* | Remove-AppxPackage -ErrorAction SilentlyContinue";
        ProcessRunResult run = await PowerShellInvoker.RunScriptAsync(script, cancellationToken).ConfigureAwait(false);
        return OperationResult.Success($"Remocao do App Installer tentada (exit {run.ExitCode}). {run.StandardOutput}{run.StandardError}");
    }
}
