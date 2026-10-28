using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Ribanense.Solucoes.App.Recuperacao.Configuration;
using Ribanense.Solucoes.App.Recuperacao.Recovery.Audio;
using Ribanense.Solucoes.App.Recuperacao.Recovery.Core;
using Ribanense.Solucoes.App.Recuperacao.ViewModels;
using Ribanense.Solucoes.Infrastructure.Logging;
using Ribanense.Solucoes.Infrastructure.Vault;
using Ribanense.Solucoes.PluginSDK.Logging;

namespace Ribanense.Solucoes.App.Recuperacao;

public partial class App : Application
{
    private const string MutexName = @"Global\Ribanense.com.ribanense.recuperacao";
    private const string AppComponent = "App.Recuperacao";

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
        string? autoRun = null;
        bool elevado = false;
        for (int i = 0; i < e.Args.Length; i++)
        {
            string a = e.Args[i].ToLowerInvariant();

            if (a == "--version")
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
                string versao = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
                Console.WriteLine($"{{\"version\":\"{versao}\",\"sdk\":\"1.0.0\"}}");
                Shutdown(0);
                return;
            }

            if (a == "--selfcheck")
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
                Console.WriteLine("ok");
                Shutdown(0);
                return;
            }

            if (a == "--logs")
            {
                AttachConsole(ATTACH_PARENT_PROCESS);
                int count = 100;
                if (i + 1 < e.Args.Length && int.TryParse(e.Args[i + 1], out int n) && n > 0)
                {
                    count = n;
                }
                var pathsDump = RecuperacaoAppConfig.Resolve();
                int logsExit = LogDumpHelper.DumpToConsole(pathsDump.VaultPath, count);
                Shutdown(logsExit);
                return;
            }

            if (a == "--run" && i + 1 < e.Args.Length)
            {
                autoRun = e.Args[++i];
                continue;
            }

            if (a == "--elevated")
            {
                elevado = true;
            }
        }

        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: false, MutexName, out _);
        try { _singleInstanceMutex.WaitOne(0, false); } catch (AbandonedMutexException) { }

        var paths = RecuperacaoAppConfig.Resolve();
        _vault = new LiteDbVault(paths.VaultPath);
        _logger = new AppJsonLogWriter(_vault);
        _logger.Write(AppLogLevel.Information, "startup",
            $"Recuperação do Windows iniciado em {paths.AppData}{(elevado ? " (elevado)" : "")}.");

        IElevationService elevation = new ElevationService();
        IConfirmDialog confirm = new MessageBoxConfirm();

        MainWindowViewModel? mainVm = null;
        Action<string> log = line => mainVm?.AppendLog(line);

        var audioVm = new AudioViewModel(paths, log, confirm, elevation);
        mainVm = new MainWindowViewModel(audioVm);
        mainVm.AppendLog("Recuperação do Windows pronto.");
        mainVm.AppendLog($"Pasta da execução: {audioVm.PastaExecucao}");
        if (elevation.IsAdmin)
        {
            mainVm.AppendLog("Instância com privilégios de administrador: todas as fases disponíveis.");
        }
        else
        {
            mainVm.AppendLog("Instância sem elevação: diagnóstico disponível; reparos pedem \"Reabrir como administrador\".");
        }

        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        if (autoRun is not null)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
                () => audioVm.RequestAutoStart(autoRun));
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _logger?.Write(AppLogLevel.Information, "shutdown", "Recuperação do Windows encerrado.");
        }
        catch { }
        _vault?.Dispose();
        try { _singleInstanceMutex?.ReleaseMutex(); } catch { }
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    /// <summary>Confirmações do fluxo de recuperação via MessageBox (Yes/No).</summary>
    private sealed class MessageBoxConfirm : IConfirmDialog
    {
        public Task<bool> ConfirmAsync(string titulo, string mensagem) =>
            Task.FromResult(
                MessageBox.Show(mensagem, titulo, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
                == MessageBoxResult.Yes);
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
                        "Recuperação do Windows",
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
