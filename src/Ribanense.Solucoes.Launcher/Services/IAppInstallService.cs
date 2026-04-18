using Ribanense.Solucoes.Launcher.Domain;

namespace Ribanense.Solucoes.Launcher.Services;

public interface IAppInstallService
{
    Task<AppInstallResult> InstallAsync(AppInstallRequest request, CancellationToken ct);
    AppUninstallResult Uninstall(string aplicativosRoot, string appId);
}
