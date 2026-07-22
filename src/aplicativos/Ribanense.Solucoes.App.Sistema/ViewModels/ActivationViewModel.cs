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

    public ActivationViewModel(IMasRunner runner, IAppJsonLog? logger = null)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _logger = logger;
        Methods = MasMethod.All;
        RunCommand = new AsyncRelayCommand(p => RunAsync((MasMethod)p!), p => !IsBusy && p is MasMethod);
    }

    public IReadOnlyList<MasMethod> Methods { get; }

    /// <summary>
    /// Define o sink de log da UI (geralmente MainWindowViewModel.AppendLog).
    /// Chamado apos a MainWindow ser construida para evitar dependencia circular.
    /// </summary>
    public void AttachUiLog(Action<string> logSink) => _logSink = logSink;

    private bool _isBusy;
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

    private void Log(string line)
    {
        try { _logger?.Write(AppLogLevel.Information, "activation", line); } catch { }
        _logSink?.Invoke(line);
    }

    private async Task RunAsync(MasMethod method)
    {
        IsBusy = true;
        Log($"Iniciando: {method.Display}...");
        try
        {
            var progress = new Progress<string>(line => Log(line));
            var result = await _runner.RunAsync(method, progress, CancellationToken.None).ConfigureAwait(true);
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
            IsBusy = false;
        }
    }
}
