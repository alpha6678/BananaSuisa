using System.Windows;
using BananaSuisa.App.ViewModels;
using BananaSuisa.Infrastructure.Diagnostics;
using BananaSuisa.Infrastructure.WinGet;
using BananaSuisa.Infrastructure.Workspace;

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
        var projectRootLocator = new ProjectRootLocator();
        var workspaceBootstrapService = new WorkspaceBootstrapService();
        var wingetLocator = new WingetLocator();
        var diagnosticsService = new RuntimeDiagnosticsService(projectRootLocator, workspaceBootstrapService, wingetLocator);
        var snapshot = diagnosticsService.Collect(AppContext.BaseDirectory);

        return MainWindowViewModel.FromSnapshot(snapshot);
    }
}