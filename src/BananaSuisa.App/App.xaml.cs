using System.IO;
using System.Windows;
using BananaSuisa.App.Logging;
using BananaSuisa.Core.Logging;
using BananaSuisa.Infrastructure.Logging;
using BananaSuisa.Services.Abstractions;

namespace BananaSuisa.App;

public partial class App : Application
{
    public App()
    {
        this.DispatcherUnhandledException += (s, e) =>
        {
            LogCrash(e.Exception);
            string path = AppJsonLogPathResolver.Resolve(AppContext.BaseDirectory);
            MessageBox.Show(
                $"Excecao nao tratada:\n\n{e.Exception.Message}\n\nLog JSON: {path}",
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
                string path = AppJsonLogPathResolver.Resolve(AppContext.BaseDirectory);
                MessageBox.Show(
                    $"Excecao no dominio da aplicacao:\n\n{ex.Message}\n\nLog JSON: {path}",
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
                string path = AppJsonLogPathResolver.Resolve(AppContext.BaseDirectory);
                IAppJsonLog log = AppJsonLogRegistry.TryGet() ?? new AppJsonLogWriter(path);
                log.Write(AppLogLevel.Warning, "task.unobserved", e.Exception.Message, e.Exception);
            }
            catch
            {
                // Evita recursao.
            }
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        string path = AppJsonLogPathResolver.Resolve(AppContext.BaseDirectory);
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var log = new AppJsonLogWriter(path);
        AppJsonLogRegistry.Initialize(log);
        log.Write(AppLogLevel.Information, "startup", "Sessao iniciada.", null);

        base.OnStartup(e);
    }

    private static void LogCrash(Exception ex)
    {
        try
        {
            string path = AppJsonLogPathResolver.Resolve(AppContext.BaseDirectory);
            IAppJsonLog log = AppJsonLogRegistry.TryGet() ?? new AppJsonLogWriter(path);
            log.Write(AppLogLevel.Critical, "unhandled", ex.Message, ex);
        }
        catch
        {
        }
    }
}
