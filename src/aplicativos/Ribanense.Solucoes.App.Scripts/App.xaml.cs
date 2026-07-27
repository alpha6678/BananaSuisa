using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Ribanense.Solucoes.App.Scripts.Configuration;
using Ribanense.Solucoes.App.Scripts.Scripts;
using Ribanense.Solucoes.App.Scripts.Scripts.Commands;
using Ribanense.Solucoes.App.Scripts.Scripts.Dns;
using Ribanense.Solucoes.App.Scripts.ViewModels;
using Ribanense.Solucoes.Infrastructure.Logging;
using Ribanense.Solucoes.Infrastructure.Vault;
using Ribanense.Solucoes.PluginSDK;
using Ribanense.Solucoes.PluginSDK.Logging;

namespace Ribanense.Solucoes.App.Scripts;

public partial class App : Application
{
    private const string MutexName = @"Global\Ribanense.com.ribanense.scripts";
    private const string AppComponent = "App.Scripts";

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

        var paths = ScriptsAppConfig.Resolve();
        _vault = new LiteDbVault(paths.VaultPath);
        _logger = new AppJsonLogWriter(_vault);
        _logger.Write(AppLogLevel.Information, "startup", $"Scripts iniciado em {paths.AppData}.");

        var dnsViewModel = new DnsBenchmarkViewModel(
            new DnsBenchmarkService(new UdpDnsQueryProbe()),
            new NetworkDnsDetector(),
            new CommandSequenceRunner(),
            _logger);

        var dnsScript = new ScriptListItemViewModel(
            new ScriptDefinition(
                id: "com.ribanense.scripts.dns-benchmark",
                name: "Testar melhor DNS",
                description: "Mede a latência de servidores DNS públicos, personalizados e o DNS atual da rede, e indica o mais rápido.",
                category: "Rede"),
            dnsViewModel);

        var viewModel = new MainWindowViewModel(new[] { dnsScript });

        var window = new MainWindow { DataContext = viewModel };
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _logger?.Write(AppLogLevel.Information, "shutdown", "Scripts encerrado."); } catch { }
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
                Console.WriteLine($"{{\"version\":\"{AppVersion.ForEntry()}\",\"sdk\":\"{SdkVersion.Current}\"}}");
                return 0;
            }

            if (a == "--selfcheck")
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
                Console.WriteLine("ok");
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
                var paths = ScriptsAppConfig.Resolve();
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
                        "Scripts",
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
