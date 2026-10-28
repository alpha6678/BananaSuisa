using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Recuperacao.ViewModels;

/// <summary>Trava simples: uma operação destrutiva/longa por vez no módulo.</summary>
public sealed class BusyGate
{
    public bool IsBusy { get; private set; }

    public bool TryEnter()
    {
        if (IsBusy) return false;
        IsBusy = true;
        return true;
    }

    public void Exit() => IsBusy = false;
}

/// <summary>
/// Base das fases do módulo: guarda busy/cancelamento, roteia log para a UI e
/// protege o fluxo contra exceções (a UI nunca morre por falha de ação).
/// </summary>
public abstract class FaseViewModelBase : ObservableObject
{
    private readonly BusyGate _gate;
    private readonly Action<string> _log;
    private CancellationTokenSource? _cts;
    private bool _isBusy;
    private string _progresso = string.Empty;

    protected FaseViewModelBase(BusyGate gate, Action<string> log)
    {
        _gate = gate;
        _log = log;
        CancelarCommand = new RelayCommand(Cancelar, () => IsBusy);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    public string Progresso
    {
        get => _progresso;
        protected set => SetProperty(ref _progresso, value);
    }

    public ICommand CancelarCommand { get; }

    protected CancellationToken CancellationPending => _cts?.Token ?? default;

    protected void Log(string mensagem) => _log(mensagem);

    protected async Task ExecutarAsync(string tituloAcao, Func<CancellationToken, Task> trabalho)
    {
        if (!_gate.TryEnter())
        {
            Log("Já existe uma operação em andamento. Aguarde ou cancele.");
            return;
        }

        _cts = new CancellationTokenSource();
        IsBusy = true;
        Progresso = tituloAcao;
        try
        {
            await trabalho(_cts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            Log($"Operação cancelada: {tituloAcao}.");
        }
        catch (Exception ex)
        {
            Log($"[ERRO] {tituloAcao} abortada: {ex.Message}");
            Log("Retornando (nada além do passo interrompido foi tocado).");
        }
        finally
        {
            _gate.Exit();
            _cts.Dispose();
            _cts = null;
            IsBusy = false;
            Progresso = string.Empty;
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    private void Cancelar()
    {
        try { _cts?.Cancel(); } catch { }
        Log("Cancelamento solicitado...");
    }

    /// <summary>Roda uma ação no dispatcher (para atualizações de UI vindas de threads).</summary>
    protected static void Post(Action action) =>
        Dispatcher.CurrentDispatcher.Invoke(action);
}
