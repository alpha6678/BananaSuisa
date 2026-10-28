using System.Collections.ObjectModel;
using System.Windows.Input;
using Ribanense.Solucoes.App.Recuperacao.Recovery.Audio;
using Ribanense.Solucoes.App.Recuperacao.Recovery.Core;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Recuperacao.ViewModels;

/// <summary>Fase 2: ponto de restauração + export de drivers + export de registro.</summary>
public sealed class AudioBackupViewModel : FaseViewModelBase
{
    private readonly IAudioBackupService _service;
    private readonly RunContext _run;
    private readonly IConfirmDialog _confirm;

    public AudioBackupViewModel(
        BusyGate gate, Action<string> log, IAudioBackupService service, RunContext run, IConfirmDialog confirm)
        : base(gate, log)
    {
        _service = service;
        _run = run;
        _confirm = confirm;
        ExecutarCommand = new AsyncRelayCommand(() => ExecutarFluxoAsync(), () => !gate.IsBusy);
    }

    public ICommand ExecutarCommand { get; }

    public ObservableCollection<BackupItem> Itens { get; } = [];

    public string StatusText { get; private set; } = "Backup não executado nesta sessão.";

    public string? CaminhoManifesto { get; private set; }

    public void ExecutarAsyncPublic() => _ = ExecutarFluxoAsync();

    private async Task ExecutarFluxoAsync()
    {
        await ExecutarAsync("Backup", async ct =>
        {
            Itens.Clear();
            var progress = new Progress<OperationEvent>(e =>
            {
                Log(OperationEventSink.Format(e));
                if (e.Kind == OperationEventKind.Info) Progresso = e.Message;
            });

            BackupResultado resultado = await _service
                .RunAsync(_run, _confirm, progress, ct).ConfigureAwait(true);

            foreach (BackupItem item in resultado.Itens)
            {
                Itens.Add(item);
            }

            StatusText = resultado.Status switch
            {
                BackupStatusGeral.Completo => "Backup COMPLETO — o Reparo pode rodar.",
                BackupStatusGeral.Incompleto => "Backup INCOMPLETO — resolva os itens com falha antes do Reparo.",
                _ => "Backup não executado.",
            };
            CaminhoManifesto = resultado.CaminhoManifesto ?? string.Empty;
        }).ConfigureAwait(true);
    }
}
