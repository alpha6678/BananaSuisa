using System.Windows.Input;
using BananaSuisa.App.Views;
using BananaSuisa.Core.Diagnostics;

namespace BananaSuisa.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private object? _currentView;
    private bool _isLoading;
    private string _loadingMessage = "Carregando...";

    public object? CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string LoadingMessage
    {
        get => _loadingMessage;
        set => SetProperty(ref _loadingMessage, value);
    }

    public ICommand NavigateCommand { get; }

    private MainWindowViewModel(
        string title,
        string subtitle,
        string baseDirectory,
        string projectRoot,
        string resourcesRoot,
        string memoryRoot,
        string dataRoot,
        string configPath,
        string configurationSourcePath,
        string configurationSummary,
        string searchSummary,
        string searchPreviewQuery,
        IReadOnlyList<SearchPreviewItemViewModel> searchPreviewItems,
        string catalogSummary,
        string catalogSearchSummary,
        string catalogSearchPreviewQuery,
        IReadOnlyList<SearchPreviewItemViewModel> catalogSearchPreviewItems,
        string wingetPath,
        string workspaceSummary,
        IReadOnlyList<DiagnosticCheckViewModel> workspaceItems,
        string generatedAt,
        IReadOnlyList<DiagnosticCheckViewModel> checks)
    {
        Title = title;
        Subtitle = subtitle;
        BaseDirectory = baseDirectory;
        ProjectRoot = projectRoot;
        ResourcesRoot = resourcesRoot;
        MemoryRoot = memoryRoot;
        DataRoot = dataRoot;
        ConfigPath = configPath;
        ConfigurationSourcePath = configurationSourcePath;
        ConfigurationSummary = configurationSummary;
        SearchSummary = searchSummary;
        SearchPreviewQuery = searchPreviewQuery;
        SearchPreviewItems = searchPreviewItems;
        CatalogSummary = catalogSummary;
        CatalogSearchSummary = catalogSearchSummary;
        CatalogSearchPreviewQuery = catalogSearchPreviewQuery;
        CatalogSearchPreviewItems = catalogSearchPreviewItems;
        WingetPath = wingetPath;
        WorkspaceSummary = workspaceSummary;
        WorkspaceItems = workspaceItems;
        GeneratedAt = generatedAt;
        Checks = checks;

        NavigateCommand = new RelayCommand(Navigate);
        CurrentView = new DashboardView { DataContext = this };
    }

    private void Navigate(object? parameter)
    {
        if (parameter is string viewName)
        {
            if (viewName == "Dashboard")
            {
                CurrentView = new DashboardView { DataContext = this };
            }
            // Outras views podem ser implementadas aqui
        }
    }

    public string Title { get; }

    public string Subtitle { get; }

    public string BaseDirectory { get; }

    public string ProjectRoot { get; }

    public string ResourcesRoot { get; }

    public string MemoryRoot { get; }

    public string DataRoot { get; }

    public string ConfigPath { get; }

    public string ConfigurationSourcePath { get; }

    public string ConfigurationSummary { get; }

    public string SearchSummary { get; }

    public string SearchPreviewQuery { get; }

    public IReadOnlyList<SearchPreviewItemViewModel> SearchPreviewItems { get; }

    public string CatalogSummary { get; }

    public string CatalogSearchSummary { get; }

    public string CatalogSearchPreviewQuery { get; }

    public IReadOnlyList<SearchPreviewItemViewModel> CatalogSearchPreviewItems { get; }

    public string WingetPath { get; }

    public string WorkspaceSummary { get; }

    public IReadOnlyList<DiagnosticCheckViewModel> WorkspaceItems { get; }

    public string GeneratedAt { get; }

    public IReadOnlyList<DiagnosticCheckViewModel> Checks { get; }

    public static MainWindowViewModel FromSnapshot(RuntimeDiagnosticsSnapshot snapshot)
    {
        IReadOnlyList<DiagnosticCheckViewModel> checks = snapshot.Checks
            .Select(check => new DiagnosticCheckViewModel(check.Name, check.IsHealthy, check.Detail))
            .ToArray();
        IReadOnlyList<DiagnosticCheckViewModel> workspaceItems = snapshot.WorkspaceBootstrapResult?.Items
            .Select(item => new DiagnosticCheckViewModel(item.Name, item.IsHealthy, item.Detail))
            .ToArray()
            ?? [];
        IReadOnlyList<SearchPreviewItemViewModel> searchPreviewItems = snapshot.ConfigurationSearchPreview?.PreviewMatches
            .Select(match => new SearchPreviewItemViewModel(match.Kind, match.DisplayText, match.Detail))
            .ToArray()
            ?? [];
        IReadOnlyList<SearchPreviewItemViewModel> catalogSearchPreviewItems = snapshot.CatalogSearchPreview?.PreviewItems
            .Select(item => new SearchPreviewItemViewModel(
                kind: item.SourceName,
                title: item.Name,
                detail: $"{item.PackageId} | {item.Category} | {(item.IsEssential ? "Essencial" : "Opcional")}"))
            .ToArray()
            ?? [];

        string projectRoot = snapshot.WorkspacePaths?.ProjectRoot ?? "Nao localizado";
        string resourcesRoot = snapshot.WorkspacePaths?.ResourcesRoot ?? "Nao localizado";
        string memoryRoot = snapshot.WorkspacePaths?.MemoryRoot ?? "Nao localizado";
        string dataRoot = snapshot.WorkspaceBootstrapResult?.Paths.DataRoot ?? "Nao localizado";
        string configPath = snapshot.WorkspaceBootstrapResult?.Paths.ConfigPath ?? "Nao localizado";
        string configurationSourcePath = snapshot.ConfigurationLoadResult?.SourcePath ?? "Nao carregado";
        string configurationSummary = snapshot.ConfigurationLoadResult?.Summary ?? "Configuracao nao carregada.";
        string searchSummary = snapshot.ConfigurationSearchPreview?.Summary ?? "Busca ainda nao preparada.";
        string? rawSearchPreviewQuery = snapshot.ConfigurationSearchPreview?.PreviewQuery;
        string searchPreviewQuery = string.IsNullOrWhiteSpace(rawSearchPreviewQuery)
            ? "Consulta piloto indisponivel"
            : rawSearchPreviewQuery;
        string catalogSummary = snapshot.CatalogLoadResult?.Summary ?? "Catalogos nao carregados.";
        string catalogSearchSummary = snapshot.CatalogSearchPreview?.Summary ?? "Busca do catalogo ainda nao preparada.";
        string? rawCatalogSearchPreviewQuery = snapshot.CatalogSearchPreview?.PreviewQuery;
        string catalogSearchPreviewQuery = string.IsNullOrWhiteSpace(rawCatalogSearchPreviewQuery)
            ? "Consulta piloto indisponivel"
            : rawCatalogSearchPreviewQuery;
        string workspaceSummary = snapshot.WorkspaceBootstrapResult is null
            ? "Workspace nao inicializado."
            : $"Pastas criadas: {snapshot.WorkspaceBootstrapResult.CreatedDirectoryCount} | Arquivos sincronizados: {snapshot.WorkspaceBootstrapResult.SynchronizedFileCount}";

        return new MainWindowViewModel(
            title: $"BananaSuisa .NET Bootstrap v{snapshot.AppVersion}",
            subtitle: "Primeiro esqueleto WPF com diagnostico de runtime, workspace e busca inicial sobre configuracao e catalogo.",
            baseDirectory: snapshot.BaseDirectory,
            projectRoot: projectRoot,
            resourcesRoot: resourcesRoot,
            memoryRoot: memoryRoot,
            dataRoot: dataRoot,
            configPath: configPath,
            configurationSourcePath: configurationSourcePath,
            configurationSummary: configurationSummary,
            searchSummary: searchSummary,
            searchPreviewQuery: searchPreviewQuery,
            searchPreviewItems: searchPreviewItems,
            catalogSummary: catalogSummary,
            catalogSearchSummary: catalogSearchSummary,
            catalogSearchPreviewQuery: catalogSearchPreviewQuery,
            catalogSearchPreviewItems: catalogSearchPreviewItems,
            wingetPath: snapshot.WingetPath ?? "Nao encontrado",
            workspaceSummary: workspaceSummary,
            workspaceItems: workspaceItems,
            generatedAt: snapshot.GeneratedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"),
            checks: checks);
    }
}
