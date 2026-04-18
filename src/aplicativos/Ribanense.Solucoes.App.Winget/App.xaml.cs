using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Ribanense.Solucoes.App.Winget.Configuration;
using Ribanense.Solucoes.App.Winget.Services;
using Ribanense.Solucoes.App.Winget.ViewModels;
using Ribanense.Solucoes.Infrastructure.Logging;
using Ribanense.Solucoes.Infrastructure.Vault;
using Ribanense.Solucoes.PluginSDK.Logging;

namespace Ribanense.Solucoes.App.Winget;

public partial class App : Application
{
    private const string MutexName = @"Global\Ribanense.com.ribanense.winget";

    private LiteDbVault? _vault;
    private AppJsonLogWriter? _logger;
    private Mutex? _singleInstanceMutex;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    private const int ATTACH_PARENT_PROCESS = -1;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Length > 0)
        {
            int cliExit = HandleCliArguments(e.Args);
            if (cliExit >= 0)
            {
                Shutdown(cliExit);
                return;
            }
        }

        base.OnStartup(e);

        // Single-instance + mutex que o Launcher observa
        _singleInstanceMutex = new Mutex(initiallyOwned: false, MutexName, out bool createdNew);
        try { _singleInstanceMutex.WaitOne(0, false); } catch (AbandonedMutexException) { }

        var paths = WingetAppConfig.Resolve();
        _vault = new LiteDbVault(paths.VaultPath);
        _logger = new AppJsonLogWriter(_vault);
        _logger.Write(AppLogLevel.Information, "startup", $"Gestor WinGet iniciado em {paths.AppData}.");

        var locator = new WingetLocator();
        var executor = new WingetExecutor(locator);
        var search = new WingetSearchService(executor);
        var list = new WingetListService(executor);
        var installer = new WingetInstallService(executor);

        var viewModel = new MainWindowViewModel(search, list, installer, locator, _logger);
        var window = new MainWindow { DataContext = viewModel };
        window.Show();

        _ = viewModel.BootstrapAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _logger?.Write(AppLogLevel.Information, "shutdown", "Gestor WinGet encerrado."); } catch { }
        _vault?.Dispose();
        try { _singleInstanceMutex?.ReleaseMutex(); } catch { }
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// Retorna o código de saída quando o argumento é reconhecido como CLI (≥ 0),
    /// ou -1 quando a aplicação deve continuar abrindo a UI.
    /// </summary>
    private static int HandleCliArguments(string[] args)
    {
        foreach (string raw in args)
        {
            string a = raw.ToLowerInvariant();
            if (a == "--version")
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
                Console.WriteLine("{\"version\":\"0.1.0\",\"sdk\":\"1.0.0\"}");
                return 0;
            }
            if (a == "--selfcheck")
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
                string? path = new WingetLocator().TryLocate();
                if (path is null)
                {
                    Console.Error.WriteLine("winget.exe não encontrado.");
                    return 1;
                }
                Console.WriteLine($"winget: {path}");
                return 0;
            }
        }
        return -1;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrash(e.Exception);
        MessageBox.Show(
            $"Erro inesperado:\n\n{e.Exception.Message}",
            "Gestor WinGet",
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
        try { _logger?.Write(AppLogLevel.Critical, "unhandled", ex.Message, ex); } catch { }
    }
}
