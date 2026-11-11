using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Ribanense.Solucoes.Infrastructure.Logging;
using Ribanense.Solucoes.Infrastructure.Vault;
using Ribanense.Solucoes.Launcher.Configuration;
using Ribanense.Solucoes.Launcher.Domain;
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
        if (TryMigrateLegacyLauncherFileNameAfterUpdate(e.Args))
        {
            Shutdown(0);
            return;
        }

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
        string? bundledPartners = LoadEmbeddedText(_logger, "Ribanense.Solucoes.Launcher.parceiros.json");
        var catalog = new CatalogService(
            _github,
            _vault,
            _logger,
            LauncherConfig.CatalogUrl,
            bundledCatalog,
            LauncherConfig.DefaultPartnersUrl,
            bundledPartners);
        var releases = new ReleaseCheckService(_github);
        var registry = new InstalledAppsRegistry();
        var installer = new AppInstallService(_github, registry, _logger, new WindowsInstallRegistration());
        var launcherUpdater = new LauncherUpdateService(releases, _github, _logger);
        var runtimeChecker = new DotNetDesktopRuntimeChecker();
        var runtimeInstaller = new DotNetDesktopRuntimeInstaller();

        var viewModel = new MainWindowViewModel(
            catalog, releases, registry, installer, launcherUpdater, _logger, LauncherConfig.AplicativosRoot,
            runtimeChecker, runtimeInstaller);

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

            if (a == "--install")
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
                LauncherDeploymentResult installed = new LauncherSelfInstaller().InstallHere();
                if (!installed.Success)
                {
                    Console.Error.WriteLine(installed.Error);
                    return 1;
                }
                Console.WriteLine(installed.ShutdownRequested
                    ? "Launcher instalado. A nova cópia foi aberta."
                    : "Launcher já estava instalado. Registro atualizado.");
                return 0;
            }

            if (a == "--uninstall")
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
                LauncherDeploymentResult removed = new LauncherSelfInstaller().Uninstall();
                if (!removed.Success)
                {
                    Console.Error.WriteLine(removed.Error);
                    return 1;
                }
                Console.WriteLine("Desinstalação concluída.");
                return 0;
            }

            if (a == "--uninstall-app" && i + 1 < args.Length)
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
                return UninstallAppFromCommandLine(args[i + 1]);
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

    private static int UninstallAppFromCommandLine(string appId)
    {
        try
        {
            var registry = new InstalledAppsRegistry();
            var registration = new InstallRegistrationService(new WindowsInstallRegistration());
            InstalledApp? installed = registry.Find(LauncherConfig.AplicativosRoot, appId);
            if (installed is null)
            {
                Console.Error.WriteLine($"App não instalado pelo launcher: {appId}");
                return 1;
            }

            if (AppProcessDetector.IsRunning(appId) &&
                !AppProcessDetector.TryCloseRunning(appId, installed.ExecutablePath, TimeSpan.FromSeconds(8)))
            {
                Console.Error.WriteLine("Não foi possível encerrar o app em execução.");
                return 1;
            }

            registration.RemoveApp(appId, installed.DisplayName);
            Directory.Delete(installed.InstallPath, recursive: true);
            Console.WriteLine($"{installed.DisplayName} removido.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
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

            if (i + 2 < args.Length)
                TryDeleteStalePath(args[i + 2]);

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (AppProcessDetector.IsRunning(LauncherConfig.LauncherAppId) && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(150);
            }
            return;
        }
    }

    /// <summary>
    /// Corrige a primeira atualizacao vinda de um launcher antigo, cujo atualizador instalava
    /// o binario novo preservando o nome versionado anterior. Renomeia o processo atual e o
    /// relanca uma unica vez com o nome correspondente a versao realmente instalada.
    /// </summary>
    private static bool TryMigrateLegacyLauncherFileNameAfterUpdate(string[] args)
    {
        if (!args.Any(a =>
                string.Equals(a, LauncherUpdateService.PostUpdateArg, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        string? currentPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentPath) || !File.Exists(currentPath))
        {
            return false;
        }

        string? targetPath;
        try
        {
            targetPath = LauncherUpdateService.GetLegacyFileNameMigrationTarget(
                currentPath,
                AppVersion.ForEntry());
        }
        catch
        {
            return false;
        }

        if (targetPath is null)
        {
            return false;
        }

        string? dir = Path.GetDirectoryName(targetPath);
        if (string.IsNullOrWhiteSpace(dir))
        {
            return false;
        }

        string? displacedTargetPath = null;
        bool currentRenamed = false;
        try
        {
            if (File.Exists(targetPath))
            {
                displacedTargetPath = targetPath + $".old-{Guid.NewGuid():N}.exe";
                File.Move(targetPath, displacedTargetPath);
            }

            File.Move(currentPath, targetPath);
            currentRenamed = true;

            var psi = new ProcessStartInfo(targetPath)
            {
                UseShellExecute = false,
                WorkingDirectory = dir
            };
            psi.ArgumentList.Add(LauncherUpdateService.PostUpdateArg);
            psi.ArgumentList.Add(Environment.ProcessId.ToString());
            psi.ArgumentList.Add(targetPath);
            Process.Start(psi);
            return true;
        }
        catch
        {
            if (currentRenamed)
            {
                TryMoveFile(targetPath, currentPath);
            }

            if (displacedTargetPath is not null)
            {
                TryMoveFile(displacedTargetPath, targetPath);
            }

            return false;
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

            string? parent = Path.GetDirectoryName(dir);
            if (string.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent)) return;
            foreach (string leftover in Directory.GetDirectories(parent))
            {
                string name = Path.GetFileName(leftover);
                if (name.Contains(".old-", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains(".new-", StringComparison.OrdinalIgnoreCase))
                {
                    TryDeleteStalePath(leftover);
                }
            }
        }
        catch
        {
            // best effort
        }
    }

    private static void TryMoveFile(string source, string destination)
    {
        try
        {
            if (File.Exists(source) && !File.Exists(destination))
            {
                File.Move(source, destination);
            }
        }
        catch
        {
            // best effort
        }
    }

    private static void TryDeleteStalePath(string path)
    {
        try
        {
            string? current = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(current) &&
                string.Equals(Path.GetFullPath(path), Path.GetFullPath(current), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            else if (File.Exists(path)) File.Delete(path);
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

    private static string? LoadEmbeddedCatalog(IAppJsonLog log) =>
        LoadEmbeddedText(log, "Ribanense.Solucoes.Launcher.catalog.json");

    private static string? LoadEmbeddedText(IAppJsonLog log, string resourceName)
    {
        try
        {
            var assembly = typeof(App).Assembly;
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null) return null;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (Exception ex)
        {
            log.Write(AppLogLevel.Warning, "catalog.embedded",
                $"Falha ao ler recurso embutido {resourceName}.", ex);
            return null;
        }
    }
}
