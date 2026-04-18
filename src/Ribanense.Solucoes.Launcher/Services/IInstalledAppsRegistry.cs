using Ribanense.Solucoes.Launcher.Domain;

namespace Ribanense.Solucoes.Launcher.Services;

public interface IInstalledAppsRegistry
{
    IReadOnlyList<InstalledApp> Scan(string aplicativosRoot);
    InstalledApp? Find(string aplicativosRoot, string appId);
}
