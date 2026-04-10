using BananaSuisa.Core.Provisioning;
using BananaSuisa.Shared.Results;

namespace BananaSuisa.Services.Abstractions;

/// <summary>
/// Foco em pacotes MSIX do ecossistema (App Installer, Loja) e integridade em Windows customizados.
/// A instalação de componentes base reutiliza o pacote oficial do GitHub (mesmo do winget).
/// </summary>
public interface IUwpAppInstallerProvisioningService
{
    Task<UwpAppInstallerProbeResult> ProbeAsync(CancellationToken cancellationToken = default);

    Task<OperationResult> InstallOrRepairAppInstallerFromOfficialBundleAsync(CancellationToken cancellationToken = default);
}
