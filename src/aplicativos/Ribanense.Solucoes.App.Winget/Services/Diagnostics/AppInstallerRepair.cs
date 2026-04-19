namespace Ribanense.Solucoes.App.Winget.Services.Diagnostics;

/// <summary>
/// Monta scripts PowerShell de reparo/instalacao e delega ao runner elevado.
/// Sem conhecimento de processo - apenas composicao de scripts.
/// </summary>
public sealed class AppInstallerRepair : IAppInstallerRepair
{
    public const string AppInstallerUrl = "https://aka.ms/getwinget";
    public const string VcLibsUrl = "https://aka.ms/Microsoft.VCLibs.x64.14.00.Desktop.appx";
    // Nota: o pacote Microsoft.UI.Xaml.2.8 precisa ser baixado do NuGet.org
    // ou de um asset do release do winget-cli. Por ora usamos o asset
    // anexado ao release mais recente do winget, que ja inclui as deps.
    public const string WingetReleasesApi = "https://api.github.com/repos/microsoft/winget-cli/releases/latest";

    private readonly IElevatedCommandRunner _elevated;

    public AppInstallerRepair(IElevatedCommandRunner elevated)
    {
        _elevated = elevated ?? throw new ArgumentNullException(nameof(elevated));
    }

    public async Task<RepairResult> ReregisterAsync(IProgress<string>? onLine, CancellationToken ct)
    {
        string script = BuildReregisterScript();
        var r = await _elevated.RunScriptAsync(script, onLine, ct).ConfigureAwait(false);
        return new RepairResult(r.Success, r.ExitCode, r.Output, r.Cancelled);
    }

    public async Task<RepairResult> DownloadAndInstallLatestAsync(IProgress<string>? onLine, CancellationToken ct)
    {
        string script = BuildDownloadInstallScript();
        var r = await _elevated.RunScriptAsync(script, onLine, ct).ConfigureAwait(false);
        return new RepairResult(r.Success, r.ExitCode, r.Output, r.Cancelled);
    }

    internal static string BuildReregisterScript()
    {
        return string.Join(Environment.NewLine,
            "$packages = @(",
            "  '" + AppInstallerDiagnostics.AppInstallerName + "',",
            "  '" + AppInstallerDiagnostics.VcLibsName + "',",
            "  '" + AppInstallerDiagnostics.UiXamlName + "'",
            ")",
            "foreach ($name in $packages) {",
            "    $pkg = Get-AppxPackage -Name $name | Select-Object -First 1",
            "    if (-not $pkg) {",
            "        Write-Host \"[SKIP] $name nao esta instalado.\"",
            "        continue",
            "    }",
            "    $manifest = Join-Path $pkg.InstallLocation 'AppxManifest.xml'",
            "    if (-not (Test-Path -LiteralPath $manifest)) {",
            "        Write-Host \"[ERRO] Manifest nao encontrado: $manifest\"",
            "        continue",
            "    }",
            "    Write-Host \"[RE-REGISTER] $name\"",
            "    Add-AppxPackage -DisableDevelopmentMode -Register $manifest",
            "}");
    }

    internal static string BuildDownloadInstallScript()
    {
        return string.Join(Environment.NewLine,
            "$ErrorActionPreference = 'Stop'",
            "$tmp = Join-Path $env:TEMP ('ribanense-appinstaller-' + [guid]::NewGuid().ToString('N').Substring(0,8))",
            "New-Item -ItemType Directory -Force -Path $tmp | Out-Null",
            "",
            "$msixPath = Join-Path $tmp 'appinstaller.msixbundle'",
            "$vcLibsPath = Join-Path $tmp 'vclibs.appx'",
            "",
            "Write-Host '[1/3] Baixando Microsoft.DesktopAppInstaller (" + AppInstallerUrl + ")'",
            "Invoke-WebRequest -Uri '" + AppInstallerUrl + "' -OutFile $msixPath -UseBasicParsing",
            "",
            "Write-Host '[2/3] Baixando Microsoft.VCLibs (" + VcLibsUrl + ")'",
            "Invoke-WebRequest -Uri '" + VcLibsUrl + "' -OutFile $vcLibsPath -UseBasicParsing",
            "",
            "Write-Host '[3/3] Instalando via Add-AppxPackage'",
            "Add-AppxPackage -Path $vcLibsPath -ForceApplicationShutdown",
            "Add-AppxPackage -Path $msixPath -ForceApplicationShutdown",
            "",
            "Write-Host '[OK] Instalacao concluida.'");
    }
}
