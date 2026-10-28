using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Ribanense.Solucoes.App.Recuperacao.Recovery.Audio;
using Ribanense.Solucoes.App.Recuperacao.Recovery.Core;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Recuperacao.ViewModels;

/// <summary>Linha de passo do reparo na UI.</summary>
public sealed class ReparoPassoViewModel
{
    public ReparoPassoViewModel(string nome, ReparoEstado estado, string? detalhe = null)
    {
        Nome = nome;
        Estado = estado;
        Detalhe = detalhe;
    }

    public string Nome { get; }
    public ReparoEstado Estado { get; set; }
    public string? Detalhe { get; set; }
}

/// <summary>Fase 3: reparo seguro com pré-listagem de passos e status ao vivo.</summary>
public sealed class AudioReparoViewModel : FaseViewModelBase
{
    private readonly IAudioRepairService _service;
    private readonly RunContext _run;
    private readonly IConfirmDialog _confirm;
    private bool _skipSfc;
    private bool _skipDism;

    public AudioReparoViewModel(
        BusyGate gate, Action<string> log, IAudioRepairService service, RunContext run, IConfirmDialog confirm)
        : base(gate, log)
    {
        _service = service;
        _run = run;
        _confirm = confirm;
        ExecutarCommand = new AsyncRelayCommand(() => ExecutarFluxoAsync(), () => !gate.IsBusy);
        PrefillPassos(new ReparoOpcoes());
    }

    public ICommand ExecutarCommand { get; }

    public bool SkipSfc
    {
        get => _skipSfc;
        set => SetProperty(ref _skipSfc, value);
    }

    public bool SkipDism
    {
        get => _skipDism;
        set => SetProperty(ref _skipDism, value);
    }

    public ObservableCollection<ReparoPassoViewModel> Passos { get; } = [];

    public string ResultadoFinal { get; private set; } = "O reparo exige backup completo (fase 2). Nada é apagado do Driver Store.";

    public void ExecutarAsyncPublic() => _ = ExecutarFluxoAsync();

    private async Task ExecutarFluxoAsync()
    {
        await ExecutarAsync("Reparo seguro", async ct =>
        {
            var opcoes = new ReparoOpcoes(SkipSfc, SkipDism);
            Passos.Clear();
            PrefillPassos(opcoes);

            var progress = new Progress<OperationEvent>(e =>
            {
                Log(OperationEventSink.Format(e));
                if (e.Kind == OperationEventKind.Info) Progresso = e.Message;
            });

            // O serviço pode rodar em background threads: marshal para a UI.
            void OnStep(ReparoPassoStatus status) =>
                Application.Current.Dispatcher.BeginInvoke(() => AtualizarPasso(status));

            ReparoResultado resultado = await _service
                .RunAsync(_run, opcoes, _confirm, OnStep, progress, ct).ConfigureAwait(true);

            ResultadoFinal = $"{resultado.PassosOk} passo(s) OK, {resultado.PassosFalhos} com problema"
                + $" — {resultado.EndpointsFinais} endpoint(s), {resultado.DispositivosSomFinais} dispositivo(s) de som.";
        }).ConfigureAwait(true);
    }

    private void PrefillPassos(ReparoOpcoes opcoes)
    {
        foreach (string nome in _service.GetStepNames(opcoes))
        {
            Passos.Add(new ReparoPassoViewModel(nome, ReparoEstado.Pendente));
        }
    }

    private void AtualizarPasso(ReparoPassoStatus status)
    {
        int index = Passos.IndexOf(Passos.FirstOrDefault(p => p.Nome == status.Nome)!);
        var novo = new ReparoPassoViewModel(status.Nome, status.Estado, status.Detalhe);
        if (index >= 0) Passos[index] = novo;
        else Passos.Add(novo);
    }
}
