using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Ribanense.Solucoes.App.Farol.Analysis;
using Ribanense.Solucoes.App.Farol.Collectors;
using Ribanense.Solucoes.App.Farol.Configuration;
using Ribanense.Solucoes.App.Farol.Mesh;
using Ribanense.Solucoes.App.Farol.Services;
using Ribanense.Solucoes.App.Farol.ViewModels;
using Ribanense.Solucoes.Infrastructure.Logging;
using Ribanense.Solucoes.Infrastructure.Vault;
using Ribanense.Solucoes.PluginSDK;
using Ribanense.Solucoes.PluginSDK.Logging;

namespace Ribanense.Solucoes.App.Farol;

public partial class App : Application
{
    private const string MutexName = @"Global\Ribanense.com.ribanense.farol";
    private const string AppComponent = "App.Farol";

    private LiteDbVault? _vault;
    private AppJsonLogWriter? _logger;
    private Mutex? _singleInstanceMutex;
    private FarolStation? _station;
    private TrayIconController? _tray;
    private MainWindow? _window;
    private MapViewModel? _map;
    private DossierViewModel? _dossier;
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

        // A janela some para a bandeja em vez de encerrar o processo, então o
        // ciclo de vida do app não pode depender da janela principal.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _singleInstanceMutex = new Mutex(initiallyOwned: false, MutexName, out _);
        try { _singleInstanceMutex.WaitOne(0, false); } catch (AbandonedMutexException) { }

        AppPaths paths = FarolAppConfig.Resolve();
        _vault = new LiteDbVault(paths.VaultPath);
        _logger = new AppJsonLogWriter(_vault);
        _logger.Write(AppLogLevel.Information, "startup", $"Farol iniciado em {paths.AppData}.");

        _station = BuildStation(paths, _logger);
        _station.StartMesh();

        BuildUserInterface(_station, paths, _logger);

        bool startHidden = e.Args.Any(a => string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase));
        if (!startHidden) ShowMainWindow();
    }

    private FarolStation BuildStation(AppPaths paths, IAppJsonLog logger)
    {
        var pairing = new PairingStore(_vault!);
        var store = new BundleStore(Path.Combine(paths.AppData, "bundles"));

        var collectors = new ICollector[]
        {
            new IdentityCollector(),
            new NetworkCollector(),
            new DiskCollector(),
            new ServicesCollector(),
            new PrintersCollector(),
            new EventLogCollector(),
            new RibanenseLogsCollector(),
            new ProcessCollector(),
        };

        return new FarolStation(
            pairing,
            store,
            new BundleCollector(collectors),
            new FindingEngine(),
            logger,
            AppVersion.ForEntry());
    }

    private void BuildUserInterface(FarolStation station, AppPaths paths, IAppJsonLog logger)
    {
        _map = new MapViewModel(station, Dispatcher);
        _dossier = new DossierViewModel(station, new BundleExporter(), new RuleBasedExplainer(), logger);

        var compare = new CompareViewModel(station, _map, new BundleComparer());

        string executable = Environment.ProcessPath ?? Path.Combine(paths.AppHome, "Ribanense.Solucoes.App.Farol.exe");

        var settings = new SettingsViewModel(
            station,
            new AutostartRegistrar(executable),
            new FirewallRuleInstaller(
                new ElevatedScriptRunner(),
                FarolAppConfig.DiscoveryPort,
                FarolAppConfig.PeerPort),
            logger);

        var main = new MainWindowViewModel(new[]
        {
            new SectionViewModel("Mapa", "Faróis vistos nesta rede local.", _map),
            new SectionViewModel("Dossiê", "Evidências desta máquina e o que elas indicam.", _dossier),
            new SectionViewModel("Comparar", "Diff contra um farol irmão da rede.", compare),
            new SectionViewModel("Ajustes", "Código da loja, bandeja e firewall.", settings),
        });

        _window = new MainWindow { DataContext = main };

        _tray = new TrayIconController(
            onOpen: ShowMainWindow,
            onCapture: CaptureFromTray,
            onExit: ExitApplication);

        station.Captured += result =>
        {
            int high = result.Findings.Count(f => f.Severity == Domain.FindingSeverity.High);
            if (high == 0) return;

            Dispatcher.BeginInvoke(() => _tray?.Notify(
                "Farol encontrou algo grave",
                RuleBasedExplainer.Headline(result.Findings)));
        };
    }

    private void ShowMainWindow()
    {
        if (_window is null) return;

        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private void CaptureFromTray()
    {
        if (_station is null) return;

        _ = Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                await _station.CaptureAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger?.Write(AppLogLevel.Error, "capture", "Falha na captura pela bandeja.", ex);
            }
        });
    }

    private void ExitApplication()
    {
        if (_window is not null) _window.CloseToTray = false;
        Shutdown(0);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _logger?.Write(AppLogLevel.Information, "shutdown", "Farol encerrado."); } catch { }

        _map?.Detach();
        _tray?.Dispose();
        _station?.Dispose();
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
                Console.WriteLine(DescribePorts());
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
                AppPaths paths = FarolAppConfig.Resolve();
                return LogDumpHelper.DumpToConsole(paths.VaultPath, count);
            }
        }

        return -1;
    }

    /// <summary>
    /// Selfcheck reporta se as portas da malha estão livres. Porta ocupada é o
    /// diagnóstico mais frequente de "os faróis não se enxergam".
    /// </summary>
    private static string DescribePorts()
    {
        bool udp = IsUdpFree(FarolAppConfig.DiscoveryPort);
        bool tcp = IsTcpFree(FarolAppConfig.PeerPort);

        if (udp && tcp) return $"ok (UDP {FarolAppConfig.DiscoveryPort} e TCP {FarolAppConfig.PeerPort} livres)";

        var busy = new List<string>();
        if (!udp) busy.Add($"UDP {FarolAppConfig.DiscoveryPort}");
        if (!tcp) busy.Add($"TCP {FarolAppConfig.PeerPort}");

        return $"ok (porta em uso: {string.Join(", ", busy)} — provavelmente outro Farol já está rodando)";
    }

    private static bool IsUdpFree(int port)
    {
        try
        {
            using var probe = new UdpClient(AddressFamily.InterNetwork);
            probe.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static bool IsTcpFree(int port)
    {
        TcpListener? probe = null;
        try
        {
            probe = new TcpListener(IPAddress.Any, port);
            probe.Start();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        finally
        {
            try { probe?.Stop(); } catch { }
        }
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
                        "Farol",
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
