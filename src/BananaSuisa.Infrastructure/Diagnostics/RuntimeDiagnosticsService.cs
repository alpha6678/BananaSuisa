using BananaSuisa.Core.Diagnostics;
using BananaSuisa.Core.Versioning;
using BananaSuisa.Core.Workspace;
using BananaSuisa.Services.Abstractions;

namespace BananaSuisa.Infrastructure.Diagnostics;

public sealed class RuntimeDiagnosticsService : IRuntimeDiagnosticsService
{
    private readonly IProjectRootLocator _projectRootLocator;
    private readonly IWingetLocator _wingetLocator;

    public RuntimeDiagnosticsService(IProjectRootLocator projectRootLocator, IWingetLocator wingetLocator)
    {
        _projectRootLocator = projectRootLocator;
        _wingetLocator = wingetLocator;
    }

    public RuntimeDiagnosticsSnapshot Collect(string startPath)
    {
        string baseDirectory = Path.GetFullPath(startPath);
        string? projectRoot = _projectRootLocator.TryLocateFrom(baseDirectory);
        WorkspacePaths? workspacePaths = projectRoot is null ? null : WorkspacePaths.FromProjectRoot(projectRoot);
        string? wingetPath = _wingetLocator.TryLocate();

        List<DiagnosticCheck> checks =
        [
            new("Sistema operacional Windows", OperatingSystem.IsWindows(), OperatingSystem.IsWindows()
                ? "Runtime desktop Windows detectada."
                : "O BananaSuisa .NET foi desenhado para Windows."),
            new("Raiz do projeto localizada", projectRoot is not null, projectRoot ?? "Nao foi possivel localizar a raiz do BananaSuisa."),
            new("Pasta BananaSuisa_recursos", workspacePaths is not null && Directory.Exists(workspacePaths.ResourcesRoot),
                workspacePaths?.ResourcesRoot ?? "Sem raiz de projeto para validar recursos."),
            new("Pasta BananaSuisa_memoria", workspacePaths is not null && Directory.Exists(workspacePaths.MemoryRoot),
                workspacePaths?.MemoryRoot ?? "Sem raiz de projeto para validar memoria."),
            new("Winget disponivel", wingetPath is not null, wingetPath ?? "winget.exe nao foi encontrado em LOCALAPPDATA ou PATH.")
        ];

        return new RuntimeDiagnosticsSnapshot(
            AppVersion.Value,
            baseDirectory,
            workspacePaths,
            wingetPath,
            checks,
            DateTime.UtcNow);
    }
}
