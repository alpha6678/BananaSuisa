using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
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

        // Se este processo foi iniciado por uma auto-atualizacao, aguarda o processo antigo
        // encerrar (liberando o mutex) antes de seguir, e limpa binarios residuais.
        WaitForPreviousInstanceAfterUpdate(e.Args);
        CleanupStaleUpdateFiles();

        string launcherMutexName = AppProcessDetector.MutexNameFor(LauncherConfig.LauncherAppId);
        _singleInstanceMutex = new Mutex(initiallyOwned: true, launcherMutexName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "O Launcher já está em execução (ou outro processo está usando os mesmos dados).\n\n" +
                "Feche a outra janela do Ribanense Soluções ou encerre o processo \"Ribanense.Solucoes.Launcher\" no Gerenciador de tarefas e tente de novo.\n\n" +
                "Se o Windows Defender acabou de analisar o aplicativo, aguarde alguns segundos antes de abrir novamente.",
                "Ribanense Soluções",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            try { _singleInstanceMutex.Dispose(); } catch { }
            _singleInstanceMutex = null;
            Shutdown(0);
            return;
        }

        Directory.CreateDirectory(LauncherConfig.LauncherDataRoot);
        Directory.CreateDirectory(LauncherConfig.AplicativosRoot);

        _vault = LiteDbVault.OpenWithRetry(LauncherConfig.LauncherVaultPath);
        _logger = new AppJsonLogWriter(_vault);
        _logger.Write(AppLogLevel.Information, "startup", "Launcher iniciado.");

        _github = new GitHubClient();
        string? bundledCatalog = LoadEmbeddedCatalog(_logger);
        var catalog = new CatalogService(_github, _vault, _logger, LauncherConfig.CatalogUrl, bundledCatalog);
        var releases = new ReleaseCheckService(_github);
        var registry = new InstalledAppsRegistry();
        var installer = new AppInstallService(_github, registry, _logger);
        var launcherUpdater = new LauncherUpdateService(releases, _github, _logger);

        var viewModel = new MainWindowViewModel(
            catalog, releases, registry, installer, launcherUpdater, _logger, LauncherConfig.AplicativosRoot);

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

        try { _singleInstanceMutex?.ReleaseMutex(); } catch { }
        try { _singleInstanceMutex?.Dispose(); } catch { }
        _singleInstanceMutex = null;

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

    /// <summary>
    /// Quando iniciado com <c>--post-update &lt;pidAntigo&gt; &lt;exeAntigo&gt;</c>, aguarda o processo
    /// antigo encerrar e o mutex de instancia unica liberar, para evitar conflito na inicializacao.
    /// </summary>
    private static void WaitForPreviousInstanceAfterUpdate(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], LauncherUpdateService.PostUpdateArg, StringComparison.OrdinalIgnoreCase))
                continue;

            if (i + 1 < args.Length && int.TryParse(args[i + 1], out int oldPid) && oldPid > 0)
            {
                try
                {
                    using var old = Process.GetProcessById(oldPid);
                    old.WaitForExit(10_000);
                }
                catch
                {
                    // processo ja encerrou ou nao existe mais
                }
            }

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (AppProcessDetector.IsRunning(LauncherConfig.LauncherAppId) && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(150);
            }
            return;
        }
    }

    /// <summary>
    /// Remove binarios residuais de auto-atualizacao (<c>*.old-*.exe</c> / <c>*.new-*.exe</c>)
    /// deixados ao lado do executavel atual. Best-effort.
    /// </summary>
    private static void CleanupStaleUpdateFiles()
    {
        try
        {
            string? exePath = Environment.ProcessPath;
            string? dir = exePath is null ? null : Path.GetDirectoryName(exePath);
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return;

            foreach (string pattern in new[] { "*.old-*.exe", "*.new-*.exe" })
            {
                foreach (string file in Directory.GetFiles(dir, pattern))
                {
                    try { File.Delete(file); } catch { /* pode estar em uso; ignora */ }
                }
            }
        }
        catch
        {
            // best effort
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
