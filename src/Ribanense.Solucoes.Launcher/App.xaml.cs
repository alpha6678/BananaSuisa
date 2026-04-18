using System.IO;
using System.Windows;
using System.Windows.Threading;
using Ribanense.Solucoes.Infrastructure.Logging;
using Ribanense.Solucoes.Infrastructure.Vault;
using Ribanense.Solucoes.Launcher.Configuration;
using Ribanense.Solucoes.Launcher.Services;
using Ribanense.Solucoes.Launcher.ViewModels;
using Ribanense.Solucoes.PluginSDK.Logging;
using Ribanense.Solucoes.PluginSDK.Vault;

namespace Ribanense.Solucoes.Launcher;

public partial class App : Application
{
    private LiteDbVault? _vault;
    private GitHubClient? _github;
    private AppJsonLogWriter? _logger;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Directory.CreateDirectory(LauncherConfig.LauncherDataRoot);
        Directory.CreateDirectory(LauncherConfig.AplicativosRoot);

        _vault = new LiteDbVault(LauncherConfig.LauncherVaultPath);
        _logger = new AppJsonLogWriter(_vault);
        _logger.Write(AppLogLevel.Information, "startup", "Launcher iniciado.");

        _github = new GitHubClient();
        var catalog = new CatalogService(_github, _vault, _logger, LauncherConfig.CatalogUrl);
        var releases = new ReleaseCheckService(_github);
        var registry = new InstalledAppsRegistry();
        var installer = new AppInstallService(_github, registry, _logger);

        var viewModel = new MainWindowViewModel(
            catalog, releases, registry, installer, _logger, LauncherConfig.AplicativosRoot);

        var window = new MainWindow { DataContext = viewModel };
        window.Show();

        _ = viewModel.BootstrapAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _logger?.Write(AppLogLevel.Information, "shutdown", "Launcher encerrado.");
        }
        catch { }

        _github?.Dispose();
        _vault?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrash(e.Exception);
        MessageBox.Show(
            $"Erro inesperado:\n\n{e.Exception.Message}",
            "Ribanense Soluções",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex) LogCrash(ex);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        LogCrash(e.Exception);
    }

    private void LogCrash(Exception ex)
    {
        try
        {
            _logger?.Write(AppLogLevel.Critical, "unhandled", ex.Message, ex);
        }
        catch { }
    }
}
