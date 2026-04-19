using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Ribanense.Solucoes.Infrastructure.Logging;
using Ribanense.Solucoes.Infrastructure.Vault;
using Ribanense.Solucoes.Launcher.Configuration;
using Ribanense.Solucoes.Launcher.Services;
using Ribanense.Solucoes.Launcher.ViewModels;
using Ribanense.Solucoes.PluginSDK;
using Ribanense.Solucoes.PluginSDK.Logging;
using Ribanense.Solucoes.PluginSDK.Vault;

namespace Ribanense.Solucoes.Launcher;

public partial class App : Application
{
    private const string LauncherComponent = "Launcher";

    private LiteDbVault? _vault;
    private GitHubClient? _github;
    private AppJsonLogWriter? _logger;
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

        Directory.CreateDirectory(LauncherConfig.LauncherDataRoot);
        Directory.CreateDirectory(LauncherConfig.AplicativosRoot);

        _vault = new LiteDbVault(LauncherConfig.LauncherVaultPath);
        _logger = new AppJsonLogWriter(_vault);
        _logger.Write(AppLogLevel.Information, "startup", "Launcher iniciado.");

        _github = new GitHubClient();
        string? bundledCatalog = LoadEmbeddedCatalog(_logger);
        var catalog = new CatalogService(_github, _vault, _logger, LauncherConfig.CatalogUrl, bundledCatalog);
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

    /// <summary>
    /// Retorna exit code quando um argumento e' reconhecido como CLI (&gt;= 0),
    /// ou -1 quando deve continuar para a UI.
    /// </summary>
    private static int HandleCliArguments(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i].ToLowerInvariant();

            if (a == "--version")
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
                string ver = AppVersion.ForEntry();
                Console.WriteLine($"{{\"version\":\"{ver}\",\"sdk\":\"{SdkVersion.Current}\"}}");
                return 0;
            }

            if (a == "--selfcheck")
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
                try
                {
                    Directory.CreateDirectory(LauncherConfig.LauncherDataRoot);
                    Directory.CreateDirectory(LauncherConfig.AplicativosRoot);
                    Console.WriteLine($"data: {LauncherConfig.LauncherDataRoot}");
                    Console.WriteLine($"aplicativos: {LauncherConfig.AplicativosRoot}");
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"selfcheck falhou: {ex.ToChainedMessage()}");
                    return 1;
                }
            }

            if (a == "--logs")
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
                int count = 100;
                if (i + 1 < args.Length && int.TryParse(args[i + 1], out int n) && n > 0)
                {
                    count = n;
                }
                return LogDumpHelper.DumpToConsole(LauncherConfig.LauncherVaultPath, count);
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
                        "Ribanense Soluções",
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
        // 1) arquivo de texto plano (sempre disponivel, sem precisar de ferramentas)
        CrashLogWriter.Write(LauncherComponent, ex);

        // 2) log estruturado no vault, com a cadeia de Inner Exceptions na mensagem
        try
        {
            _logger?.Write(AppLogLevel.Critical, "unhandled", ex.ToChainedMessage(), ex);
        }
        catch { }
    }

    private static string? LoadEmbeddedCatalog(IAppJsonLog log)
    {
        try
        {
            var assembly = typeof(App).Assembly;
            const string resourceName = "Ribanense.Solucoes.Launcher.catalog.json";
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null) return null;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            log.Write(AppLogLevel.Warning, "catalog.embedded",
                "Falha ao ler catalogo embutido.", ex);
            return null;
        }
    }
}
