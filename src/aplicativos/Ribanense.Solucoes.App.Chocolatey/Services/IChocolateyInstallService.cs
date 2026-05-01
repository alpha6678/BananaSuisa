using Ribanense.Solucoes.App.Chocolatey.Domain;

namespace Ribanense.Solucoes.App.Chocolatey.Services;

public interface IChocolateyInstallService
{
    Task<ChocolateyRunResult> InstallAsync(string packageId, Action<string>? onLine, CancellationToken ct);
    Task<ChocolateyRunResult> UninstallAsync(string packageId, Action<string>? onLine, CancellationToken ct);
    Task<ChocolateyRunResult> UpgradeAsync(string packageId, Action<string>? onLine, CancellationToken ct);
}
