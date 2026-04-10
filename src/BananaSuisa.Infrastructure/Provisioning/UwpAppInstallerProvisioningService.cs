using BananaSuisa.Core.Provisioning;
using BananaSuisa.Services.Abstractions;
using BananaSuisa.Shared.Results;

namespace BananaSuisa.Infrastructure.Provisioning;

public sealed class UwpAppInstallerProvisioningService : IUwpAppInstallerProvisioningService
{
    private readonly IWingetProvisioningService _wingetProvisioning;

    public UwpAppInstallerProvisioningService(IWingetProvisioningService wingetProvisioning)
    {
        _wingetProvisioning = wingetProvisioning;
    }

    public async Task<UwpAppInstallerProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new UwpAppInstallerProbeResult(false, null, null, false, null, false, "Disponivel apenas no Windows.");
        }

        const string script = """
$ai = Get-AppxPackage -Name Microsoft.DesktopAppInstaller | Select-Object -First 1
if ($null -eq $ai) { Write-Output 'APPINSTALLER_NONE' } else { Write-Output ('APPINSTALLER|' + $ai.PackageFullName + '|' + $ai.Version) }
$st = Get-AppxPackage -Name Microsoft.WindowsStore | Select-Object -First 1
if ($null -eq $st) { Write-Output 'STORE_NONE' } else { Write-Output ('STORE|' + $st.Version) }
""";

        ProcessRunResult run = await PowerShellInvoker.RunScriptAsync(script, cancellationToken).ConfigureAwait(false);

        string? aiFull = null;
        string? aiVer = null;
        bool aiFound = false;
        string? storeVer = null;
        bool storeFound = false;

        foreach (string line in run.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line == "APPINSTALLER_NONE")
            {
                aiFound = false;
            }
            else if (line.StartsWith("APPINSTALLER|", StringComparison.Ordinal))
            {
                string[] parts = line.Split('|', 3, StringSplitOptions.TrimEntries);
                if (parts.Length >= 3)
                {
                    aiFound = true;
                    aiFull = parts[1];
                    aiVer = parts[2];
                }
            }
            else if (line == "STORE_NONE")
            {
                storeFound = false;
            }
            else if (line.StartsWith("STORE|", StringComparison.Ordinal))
            {
                storeFound = true;
                storeVer = line.Length > 6 ? line.Substring(6) : null;
            }
        }

        bool healthy = aiFound && !string.IsNullOrWhiteSpace(aiVer) && run.ExitCode == 0;
        string summary = healthy
            ? $"App Installer presente ({aiVer}). Loja Microsoft: {(storeFound ? storeVer : "nao encontrada (opcional em sistemas enxutos)")}."
            : "App Installer nao encontrado ou pacote incompleto. Em Windows sem componentes UWP, use Instalar para baixar o bundle oficial.";

        if (run.ExitCode != 0)
        {
            summary = $"Falha ao consultar pacotes (exit {run.ExitCode}). {run.StandardError}{run.StandardOutput}";
        }

        return new UwpAppInstallerProbeResult(aiFound, aiFull, aiVer, storeFound, storeVer, healthy, summary);
    }

    public Task<OperationResult> InstallOrRepairAppInstallerFromOfficialBundleAsync(CancellationToken cancellationToken = default)
    {
        return _wingetProvisioning.InstallLatestFromGitHubReleaseAsync(cancellationToken);
    }
}
