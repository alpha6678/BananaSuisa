using System.Net.Http;
using System.Windows;
using BananaSuisa.App.Logging;
using BananaSuisa.App.ViewModels;
using BananaSuisa.Infrastructure.Diagnostics;
using BananaSuisa.Infrastructure.Provisioning;
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
        var wingetLocator = new WingetLocator();
        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("BananaSuisa/1.0");
        var wingetProvisioning = new WingetProvisioningService(wingetLocator, http);
        var uwpProvisioning = new UwpAppInstallerProvisioningService(wingetProvisioning);
        var wingetSearch = new WingetSearchService(wingetLocator);
        var wingetPackageInstall = new WingetPackageInstallService(wingetLocator);
        var diagnosticsService = new RuntimeDiagnosticsService(projectRootLocator, wingetLocator);
        var snapshot = diagnosticsService.Collect(AppContext.BaseDirectory);

        return MainWindowViewModel.FromSnapshot(
            snapshot,
            wingetProvisioning,
            uwpProvisioning,
            wingetSearch,
            wingetPackageInstall,
            AppJsonLogRegistry.Current);
    }
}
