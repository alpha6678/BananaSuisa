using System.Collections.ObjectModel;
using System.Windows.Input;
using Ribanense.Solucoes.App.Sistema.Services;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Sistema.ViewModels;

public sealed class ActivationViewModel : ObservableObject
{
    private readonly IMasRunner _runner;
    private readonly Action<string> _appendLog;

    public ActivationViewModel(IMasRunner runner, Action<string> appendLog)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _appendLog = appendLog ?? throw new ArgumentNullException(nameof(appendLog));
        Methods = MasMethod.All;
        RunCommand = new AsyncRelayCommand(p => RunAsync((MasMethod)p!), p => !IsBusy && p is MasMethod);
    }

    public IReadOnlyList<MasMethod> Methods { get; }

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

    private MasMethod? _selectedMethod;
    public MasMethod? SelectedMethod
    {
        get => _selectedMethod;
        set
        {
            if (SetProperty(ref _selectedMethod, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public ICommand RunCommand { get; }

    private async Task RunAsync(MasMethod method)
    {
        IsBusy = true;
        _appendLog($"Iniciando: {method.Display}...");
        try
        {
            var progress = new Progress<string>(line => _appendLog(line));
            var result = await _runner.RunAsync(method, progress, CancellationToken.None).ConfigureAwait(true);
            if (result.Success)
            {
                _appendLog($"Concluído: {method.Display}.");
            }
            else if (result.Cancelled)
            {
                _appendLog($"Cancelado: {result.Error}");
            }
            else
            {
                _appendLog($"Falha: {result.Error}");
            }
        }
        catch (Exception ex)
        {
            _appendLog($"Erro: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
