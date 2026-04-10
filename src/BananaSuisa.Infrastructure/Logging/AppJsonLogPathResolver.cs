using BananaSuisa.Core.Workspace;
using BananaSuisa.Infrastructure.Workspace;
using BananaSuisa.Services.Abstractions;

namespace BananaSuisa.Infrastructure.Logging;

public static class AppJsonLogPathResolver
{
    /// <summary>
    /// Caminho do ficheiro JSON de diagnostico: em workspace (Registros/BananaSuisa.json) ou %LocalAppData%\BananaSuisa\BananaSuisa.json.
    /// </summary>
    public static string Resolve(string baseDirectory, IProjectRootLocator? locator = null)
    {
        IProjectRootLocator loc = locator ?? new ProjectRootLocator();
        string full = Path.GetFullPath(baseDirectory);
        string? projectRoot = loc.TryLocateFrom(full);
        if (projectRoot is not null)
        {
            return WorkspacePaths.FromProjectRoot(projectRoot).LogFilePath;
        }

        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BananaSuisa");
        return Path.Combine(dir, "BananaSuisa.json");
    }
}
