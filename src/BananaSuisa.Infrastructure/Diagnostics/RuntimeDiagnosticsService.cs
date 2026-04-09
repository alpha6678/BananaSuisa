using BananaSuisa.Core.Catalog;
using BananaSuisa.Core.Configuration;
using BananaSuisa.Core.Diagnostics;
using BananaSuisa.Core.Search;
using BananaSuisa.Core.Versioning;
using BananaSuisa.Core.Workspace;
using BananaSuisa.Services.Abstractions;

namespace BananaSuisa.Infrastructure.Diagnostics;

public sealed class RuntimeDiagnosticsService : IRuntimeDiagnosticsService
{
    private readonly ICatalogLoader _catalogLoader;
    private readonly ICatalogSearchService _catalogSearchService;
    private readonly IConfigurationLoader _configurationLoader;
    private readonly IConfigurationSearchService _configurationSearchService;
    private readonly IProjectRootLocator _projectRootLocator;
    private readonly IWorkspaceBootstrapService _workspaceBootstrapService;
    private readonly IWingetLocator _wingetLocator;

    public RuntimeDiagnosticsService(
        ICatalogLoader catalogLoader,
        ICatalogSearchService catalogSearchService,
        IConfigurationLoader configurationLoader,
        IConfigurationSearchService configurationSearchService,
        IProjectRootLocator projectRootLocator,
        IWorkspaceBootstrapService workspaceBootstrapService,
        IWingetLocator wingetLocator)
    {
        _catalogLoader = catalogLoader;
        _catalogSearchService = catalogSearchService;
        _configurationLoader = configurationLoader;
        _configurationSearchService = configurationSearchService;
        _projectRootLocator = projectRootLocator;
        _workspaceBootstrapService = workspaceBootstrapService;
        _wingetLocator = wingetLocator;
    }

    public RuntimeDiagnosticsSnapshot Collect(string startPath)
    {
        string baseDirectory = Path.GetFullPath(startPath);
        string? projectRoot = _projectRootLocator.TryLocateFrom(baseDirectory);
        WorkspacePaths? workspacePaths = projectRoot is null ? null : WorkspacePaths.FromProjectRoot(projectRoot);
        var workspaceBootstrapResult = workspacePaths is null
            ? null
            : _workspaceBootstrapService.EnsureInitialized(workspacePaths);
        ConfigurationLoadResult? configurationLoadResult = workspaceBootstrapResult is null
            ? null
            : _configurationLoader.Load(workspaceBootstrapResult.Paths);
        ConfigurationSearchPreview? configurationSearchPreview = configurationLoadResult?.Configuration is null
            ? null
            : _configurationSearchService.BuildPreview(configurationLoadResult.Configuration);
        CatalogLoadResult? catalogLoadResult = workspaceBootstrapResult is null
            ? null
            : _catalogLoader.Load(workspaceBootstrapResult.Paths);
        CatalogSearchPreview? catalogSearchPreview = catalogLoadResult is null
            ? null
            : _catalogSearchService.BuildPreview(catalogLoadResult);
        string? wingetPath = _wingetLocator.TryLocate();

        List<DiagnosticCheck> checks =
        [
            new("Sistema operacional Windows", OperatingSystem.IsWindows(), OperatingSystem.IsWindows()
                ? "Runtime desktop Windows detectada."
                : "O BananaSuisa .NET foi desenhado para Windows."),
            new("Raiz do projeto localizada", projectRoot is not null, projectRoot ?? "Nao foi possivel localizar a raiz do BananaSuisa."),
            new("Pasta BananaSuisa_recursos", workspacePaths is not null && Directory.Exists(workspacePaths.ResourcesRoot),
                workspacePaths?.ResourcesRoot ?? "Sem raiz de projeto para validar recursos."),
            new("Pasta BananaSuisa_memoria", workspaceBootstrapResult is not null && Directory.Exists(workspaceBootstrapResult.Paths.MemoryRoot),
                workspacePaths?.MemoryRoot ?? "Sem raiz de projeto para validar memoria."),
            new("Pasta Dados pronta", workspaceBootstrapResult is not null && Directory.Exists(workspaceBootstrapResult.Paths.DataRoot),
                workspaceBootstrapResult?.Paths.DataRoot ?? "Sem raiz de projeto para validar a pasta Dados."),
            new("Configuracao base disponivel", workspaceBootstrapResult is not null && File.Exists(workspaceBootstrapResult.Paths.ConfigPath),
                workspaceBootstrapResult?.Paths.ConfigPath ?? "Sem raiz de projeto para validar a configuracao."),
            new("Configuracao carregada", configurationLoadResult?.Succeeded == true,
                configurationLoadResult?.Summary ?? "A configuracao ainda nao foi carregada."),
            new("Busca da configuracao pronta", configurationSearchPreview is not null && configurationSearchPreview.IndexedEntryCount > 0,
                configurationSearchPreview?.Summary ?? "O indice de busca ainda nao foi preparado."),
            new("Catalogos carregados", catalogLoadResult?.Succeeded == true,
                catalogLoadResult?.Summary ?? "Os catalogos ainda nao foram carregados."),
            new("Busca do catalogo pronta", catalogLoadResult?.Succeeded == true && catalogSearchPreview is not null,
                catalogSearchPreview?.Summary ?? "A busca do catalogo ainda nao foi preparada."),
            new("Winget disponivel", wingetPath is not null, wingetPath ?? "winget.exe nao foi encontrado em LOCALAPPDATA ou PATH.")
        ];

        return new RuntimeDiagnosticsSnapshot(
            AppVersion.Value,
            baseDirectory,
            workspacePaths,
            workspaceBootstrapResult,
            configurationLoadResult,
            configurationSearchPreview,
            catalogLoadResult,
            catalogSearchPreview,
            wingetPath,
            checks,
            DateTime.UtcNow);
    }
}
