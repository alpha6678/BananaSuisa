using System.Windows;
using BananaSuisa.App.ViewModels;
using BananaSuisa.Infrastructure.Catalog;
using BananaSuisa.Infrastructure.Configuration;
using BananaSuisa.Infrastructure.Diagnostics;
using BananaSuisa.Infrastructure.WinGet;
using BananaSuisa.Infrastructure.Workspace;
using BananaSuisa.Services.Search;

namespace BananaSuisa.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = BuildViewModel();
    }

    private static MainWindowViewModel BuildViewModel()
    {
        var catalogLoader = new CatalogLoader();
        var catalogSearchService = new CatalogSearchService();
        var configurationLoader = new ConfigurationLoader();
        var configurationSearchService = new ConfigurationSearchService();
        var projectRootLocator = new ProjectRootLocator();
        var workspaceBootstrapService = new WorkspaceBootstrapService();
        var wingetLocator = new WingetLocator();
        var diagnosticsService = new RuntimeDiagnosticsService(
            catalogLoader,
            catalogSearchService,
            configurationLoader,
            configurationSearchService,
            projectRootLocator,
            workspaceBootstrapService,
            wingetLocator);
        var snapshot = diagnosticsService.Collect(AppContext.BaseDirectory);

        return MainWindowViewModel.FromSnapshot(snapshot);
    }
}