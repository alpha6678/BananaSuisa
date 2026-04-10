using BananaSuisa.Core.Provisioning;
using BananaSuisa.Shared.Results;

namespace BananaSuisa.Services.Abstractions;

/// <summary>
/// Verifica, instala ou reinstala o Windows Package Manager (winget) e o pacote App Installer a partir do release oficial no GitHub.
/// </summary>
public interface IWingetProvisioningService
{
    Task<WingetProbeResult> ProbeAsync(CancellationToken cancellationToken = default);

    Task<OperationResult> InstallLatestFromGitHubReleaseAsync(CancellationToken cancellationToken = default);

    Task<OperationResult> ReinstallAsync(CancellationToken cancellationToken = default);
}
