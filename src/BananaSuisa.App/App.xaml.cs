using System.IO;
using System.Windows;
using BananaSuisa.App.Logging;
using BananaSuisa.Core.Logging;
using BananaSuisa.Infrastructure.Logging;
using BananaSuisa.Infrastructure.Vault;
using BananaSuisa.Infrastructure.Workspace;
using BananaSuisa.Core.Workspace;
using BananaSuisa.Services.Abstractions;

namespace BananaSuisa.App;

public partial class App : Application
{
    public static IVault? Vault { get; private set; }

    public App()
    {
        this.DispatcherUnhandledException += (s, e) =>
        {
            LogCrash(e.Exception);
            MessageBox.Show(
                $"Excecao nao tratada:\n\n{e.Exception.Message}",
                "Erro critico",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogCrash(ex);
                MessageBox.Show(
                    $"Excecao no dominio da aplicacao:\n\n{ex.Message}",
                    "Erro critico",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            e.SetObserved();
            try
            {
                AppJsonLogRegistry.TryGet()?.Write(AppLogLevel.Warning, "task.unobserved", e.Exception.Message, e.Exception);
            }
            catch
            {
            }
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        var locator = new ProjectRootLocator();
        string baseDir = AppContext.BaseDirectory;
        string? projectRoot = locator.TryLocateFrom(Path.GetFullPath(baseDir));

        WorkspacePaths paths = projectRoot is not null
            ? WorkspacePaths.FromProjectRoot(projectRoot)
            : WorkspacePaths.FromBaseDirectory(baseDir);

        Vault = new LiteDbVault(paths.VaultPath);

        var log = new AppJsonLogWriter(Vault);
        AppJsonLogRegistry.Initialize(log);
        log.Write(AppLogLevel.Information, "startup", "Sessao iniciada.");

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Vault?.Dispose();
        base.OnExit(e);
    }

    private static void LogCrash(Exception ex)
    {
        try
        {
            AppJsonLogRegistry.TryGet()?.Write(AppLogLevel.Critical, "unhandled", ex.Message, ex);
        }
        catch
        {
        }
    }
}
