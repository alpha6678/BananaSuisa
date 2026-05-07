using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Ribanense.Solucoes.App.Winget.Configuration;
using Ribanense.Solucoes.App.Winget.Services;
using Ribanense.Solucoes.App.Winget.Services.Diagnostics;
using Ribanense.Solucoes.App.Winget.Services.Search;
using Ribanense.Solucoes.App.Winget.Services.Sources;
using Ribanense.Solucoes.App.Winget.ViewModels;
using Ribanense.Solucoes.Infrastructure.Logging;
using Ribanense.Solucoes.Infrastructure.Vault;
using Ribanense.Solucoes.PluginSDK.Logging;

namespace Ribanense.Solucoes.App.Winget;

public partial class App : Application
{
    private const string MutexName = @"Global\Ribanense.com.ribanense.winget";
    private const string AppComponent = "App.Winget";

    private LiteDbVault? _vault;
    private AppJsonLogWriter? _logger;
    private Mutex? _singleInstanceMutex;
    private bool _isHandlingUnhandled;

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

        _singleInstanceMutex = new Mutex(initiallyOwned: false, MutexName, out _);
        try { _singleInstanceMutex.WaitOne(0, false); } catch (AbandonedMutexException) { }

        var paths = WingetAppConfig.Resolve();
        _vault = new LiteDbVault(paths.VaultPath);
        _logger = new AppJsonLogWriter(_vault);
        _logger.Write(AppLogLevel.Information, "startup", $"Gestor WinGet iniciado em {paths.AppData}.");

        var locator = new WingetLocator();
        var executor = new WingetExecutor(locator);
        var rawSearch = new WingetSearchService(executor);
        var list = new WingetListService(executor);
        var installer = new WingetInstallService(executor);

        var elevated = new ElevatedCommandRunner();
        var sources = new WingetSourceService(executor, elevated);

        var aliasCatalog = new EmbeddedAppAliasCatalog();
        var enhancer = new AliasAwareSearchEnhancer(rawSearch, aliasCatalog);

        var powerShell = new PowerShellRunner();
        var diagnostics = new AppInstallerDiagnostics(locator, executor, powerShell);
        var repair = new AppInstallerRepair(elevated);
        var moduleVm = new ModuleViewModel(diagnostics, repair, _logger);

        var viewModel = new MainWindowViewModel(enhancer, list, installer, locator, sources, aliasCatalog, _logger)
        {
            ModuleTab = moduleVm
        };
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

    private static int HandleCliArguments(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i].ToLowerInvariant();

            if (a == "--version")
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
                Console.WriteLine("{\"version\":\"0.2.0\",\"sdk\":\"1.0.0\"}");
                return 0;
            }

            if (a == "--selfcheck")
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
                string? path = new WingetLocator().TryLocate();
                if (path is null)
                {
                    Console.Error.WriteLine("winget.exe nao encontrado.");
                    return 1;
                }
                Console.WriteLine($"winget: {path}");
                return 0;
            }

            if (a == "--logs")
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
                int count = 100;
                if (i + 1 < args.Length && int.TryParse(args[i + 1], out int n) && n > 0)
                {
                    count = n;
                }
                var paths = WingetAppConfig.Resolve();
                return LogDumpHelper.DumpToConsole(paths.VaultPath, count);
            }
        }
        return -1;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrash(e.Exception);

        if (_isHandlingUnhandled)
        {
            e.Handled = true;
            return;
        }

        _isHandlingUnhandled = true;
        try
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    MessageBox.Show(
                        "Erro inesperado:\n\n" + e.Exception.ToChainedMessage()
                            + "\n\nDetalhes em %LOCALAPPDATA%\\Ribanense Soluções\\crash.log",
                        "Gestor WinGet",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                finally
                {
                    _isHandlingUnhandled = false;
                }
            }), DispatcherPriority.ApplicationIdle);
        }
        catch
        {
            _isHandlingUnhandled = false;
        }

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
        CrashLogWriter.Write(AppComponent, ex);
        try
        {
            _logger?.Write(AppLogLevel.Critical, "unhandled", ex.ToChainedMessage(), ex);
        }
        catch { }
    }
}
