using System.Windows.Input;
using Ribanense.Solucoes.App.Sistema.Services;
using Ribanense.Solucoes.PluginSDK.Logging;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Sistema.ViewModels;

public sealed class ActivationViewModel : ObservableObject
{
    private readonly IMasRunner _runner;
    private readonly IAppJsonLog? _logger;
    private Action<string>? _logSink;

    private bool _isInteractiveMode = true;
    private bool _usePowerShell;
    private bool _isBusy;
    private string _scriptStatusText = "Verificando script MAS local...";

    public ActivationViewModel(IMasRunner runner, IAppJsonLog? logger = null)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _logger = logger;
        Methods = MasMethod.All;

        RunCommand = new AsyncRelayCommand(p => RunAsync((MasMethod)p!), p => !IsBusy && p is MasMethod);
        UpdateScriptCommand = new AsyncRelayCommand(_ => UpdateScriptAsync(), _ => !IsBusy);
        OpenInteractiveMenuCommand = new AsyncRelayCommand(_ => OpenInteractiveMenuAsync(), _ => !IsBusy);

        RefreshScriptStatus();
    }

    public IReadOnlyList<MasMethod> Methods { get; }

    public bool IsInteractiveMode
    {
        get => _isInteractiveMode;
        set
        {
            if (SetProperty(ref _isInteractiveMode, value))
            {
                OnPropertyChanged(nameof(IsDirectMode));
            }
        }
    }

    public bool IsDirectMode
    {
        get => !_isInteractiveMode;
        set
        {
            if (value)
            {
                IsInteractiveMode = false;
            }
        }
    }

    public bool UsePowerShell
    {
        get => _usePowerShell;
        set
        {
            if (SetProperty(ref _usePowerShell, value))
            {
                OnPropertyChanged(nameof(UseCmd));
            }
        }
    }

    public bool UseCmd
    {
        get => !_usePowerShell;
        set
        {
            if (value)
            {
                UsePowerShell = false;
            }
        }
    }

    public string ScriptStatusText
    {
        get => _scriptStatusText;
        private set => SetProperty(ref _scriptStatusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public ICommand RunCommand { get; }
    public ICommand UpdateScriptCommand { get; }
    public ICommand OpenInteractiveMenuCommand { get; }

    /// <summary>
    /// Define o sink de log da UI (geralmente MainWindowViewModel.AppendLog).
    /// Chamado após a MainWindow ser construída para evitar dependência circular.
    /// </summary>
    public void AttachUiLog(Action<string> logSink) => _logSink = logSink;

    public void RefreshScriptStatus()
    {
        try
        {
            var info = _runner.GetScriptInfo();
            if (info.Exists && info.LastDownloaded.HasValue)
            {
                ScriptStatusText = $"Script MAS local disponível (última atualização: {info.LastDownloaded.Value:dd/MM/yyyy HH:mm}).";
            }
            else
            {
                ScriptStatusText = "Script MAS local não encontrado. Será baixado automaticamente na primeira execução.";
            }
        }
        catch
        {
            ScriptStatusText = "Status do script MAS local indisponível.";
        }
    }

    private void Log(string line)
    {
        try { _logger?.Write(AppLogLevel.Information, "activation", line); } catch { }
        _logSink?.Invoke(line);
    }

    private async Task RunAsync(MasMethod method)
    {
        IsBusy = true;
        var engine = UsePowerShell ? MasEngine.PowerShell : MasEngine.Cmd;
        var options = new MasRunOptions(
            InteractiveTerminal: IsInteractiveMode,
            ForceRedownload: false,
            Engine: engine
        );

        string modeName = IsInteractiveMode ? "Janela de Terminal Interativa" : "Execução Direta";
        string engineName = UsePowerShell ? "PowerShell" : "CMD";
        Log($"Iniciando: {method.Display} [{modeName} / {engineName}]...");

        try
        {
            var progress = new Progress<string>(line => Log(line));
            var result = await _runner.RunAsync(method, options, progress, CancellationToken.None).ConfigureAwait(true);
            if (result.Success)
            {
                Log($"Concluído: {method.Display}.");
            }
            else if (result.Cancelled)
            {
                Log($"Cancelado: {result.Error}");
            }
            else
            {
                Log($"Falha: {result.Error}");
            }
        }
        catch (Exception ex)
        {
            Log($"Erro: {ex.Message}");
        }
        finally
        {
            RefreshScriptStatus();
            IsBusy = false;
        }
    }

    private async Task UpdateScriptAsync()
    {
        IsBusy = true;
        Log("Solicitando atualização do script MAS...");
        try
        {
            var progress = new Progress<string>(line => Log(line));
            bool success = await _runner.RedownloadScriptAsync(progress, CancellationToken.None).ConfigureAwait(true);
            if (success)
            {
                Log("Script MAS atualizado com sucesso.");
            }
            else
            {
                Log("Não foi possível atualizar o script MAS.");
            }
        }
        catch (Exception ex)
        {
            Log($"Erro ao atualizar script: {ex.Message}");
        }
        finally
        {
            RefreshScriptStatus();
            IsBusy = false;
        }
    }

    private async Task OpenInteractiveMenuAsync()
    {
        IsBusy = true;
        Log("Abrindo Menu Interativo Completo do MAS...");
        try
        {
            var engine = UsePowerShell ? MasEngine.PowerShell : MasEngine.Cmd;
            var options = new MasRunOptions(
                InteractiveTerminal: true,
                ForceRedownload: false,
                Engine: engine
            );
            var progress = new Progress<string>(line => Log(line));
            var result = await _runner.RunAsync(MasMethod.Troubleshoot, options, progress, CancellationToken.None).ConfigureAwait(true);
            if (result.Success)
            {
                Log("Sessão do Menu Interativo do MAS encerrada.");
            }
            else if (result.Cancelled)
            {
                Log($"Menu Interativo cancelado: {result.Error}");
            }
            else
            {
                Log($"Menu Interativo encerrado: {result.Error}");
            }
        }
        catch (Exception ex)
        {
            Log($"Erro ao abrir menu interativo: {ex.Message}");
        }
        finally
        {
            RefreshScriptStatus();
            IsBusy = false;
        }
    }
}
