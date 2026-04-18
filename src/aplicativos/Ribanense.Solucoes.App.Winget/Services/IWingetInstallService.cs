using Ribanense.Solucoes.App.Winget.Domain;

namespace Ribanense.Solucoes.App.Winget.Services;

public interface IWingetInstallService
{
    Task<WingetRunResult> InstallAsync(string packageId, Action<string>? onLine, CancellationToken ct);
    Task<WingetRunResult> UninstallAsync(string packageId, Action<string>? onLine, CancellationToken ct);
    Task<WingetRunResult> UpgradeAsync(string packageId, Action<string>? onLine, CancellationToken ct);
}
