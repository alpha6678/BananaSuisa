using System.IO;
using Ribanense.Solucoes.Launcher.Domain;
using Ribanense.Solucoes.PluginSDK.Manifest;

namespace Ribanense.Solucoes.Launcher.Services;

/// <summary>
/// Varre <c>aplicativos/&lt;Nome&gt;/app.json</c> para montar a lista de apps instalados.
/// Pastas sem app.json valido ou sem o .exe referenciado sao ignoradas silenciosamente.
/// </summary>
public sealed class InstalledAppsRegistry : IInstalledAppsRegistry
{
    public IReadOnlyList<InstalledApp> Scan(string aplicativosRoot)
    {
        if (string.IsNullOrWhiteSpace(aplicativosRoot) || !Directory.Exists(aplicativosRoot))
        {
            return Array.Empty<InstalledApp>();
        }

        var list = new List<InstalledApp>();

        foreach (string dir in Directory.GetDirectories(aplicativosRoot))
        {
            string manifestPath = Path.Combine(dir, "app.json");
            if (!File.Exists(manifestPath)) continue;

            AppManifest manifest;
            try
            {
                manifest = AppManifest.Load(manifestPath);
            }
            catch
            {
                continue;
            }

            if (manifest.Validate().Count > 0) continue;

            string exePath = Path.Combine(dir, manifest.EntryExecutable);
            if (!File.Exists(exePath)) continue;

            list.Add(new InstalledApp
            {
                Manifest = manifest,
                InstallPath = Path.GetFullPath(dir),
                ExecutablePath = Path.GetFullPath(exePath)
            });
        }

        return list;
    }

    public InstalledApp? Find(string aplicativosRoot, string appId)
    {
        if (string.IsNullOrWhiteSpace(appId)) return null;
        return Scan(aplicativosRoot).FirstOrDefault(a => string.Equals(a.Id, appId, StringComparison.Ordinal));
    }
}
