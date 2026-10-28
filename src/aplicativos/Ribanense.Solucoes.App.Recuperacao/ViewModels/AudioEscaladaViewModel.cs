using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Ribanense.Solucoes.App.Recuperacao.Recovery.Audio;
using Ribanense.Solucoes.App.Recuperacao.Recovery.Core;
using Ribanense.Solucoes.PluginSDK;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Recuperacao.ViewModels;

/// <summary>
/// Fase 4: escalada — remoção cirúrgica de drivers de áudio (com backup
/// obrigatório) e guia do reparo in-place.
/// </summary>
public sealed class AudioEscaladaViewModel : FaseViewModelBase
{
    private readonly IAudioEscalationService _service;
    private readonly RunContext _run;
    private readonly AppPaths _paths;
    private readonly IConfirmDialog _confirm;
    private readonly IElevationService _elevation;

    public AudioEscaladaViewModel(
        BusyGate gate,
        Action<string> log,
        IAudioEscalationService service,
        RunContext run,
        AppPaths paths,
        IConfirmDialog confirm,
        IElevationService elevation)
        : base(gate, log)
    {
        _service = service;
        _run = run;
        _paths = paths;
        _confirm = confirm;
        _elevation = elevation;

        CarregarDriversCommand = new AsyncRelayCommand(() => CarregarFluxoAsync(), () => !gate.IsBusy);
        RemoverSelecionadosCommand = new AsyncRelayCommand(() => RemoverFluxoAsync(), () => !gate.IsBusy);
    }

    public ICommand CarregarDriversCommand { get; }

    public ICommand RemoverSelecionadosCommand { get; }

    public ObservableCollection<MediaDriverViewModel> Drivers { get; } = [];

    public string GuiaInPlace => AudioEscalationService.GuiaInPlace;

    public bool TemDrivers => Drivers.Count > 0;

    public void CarregarDriversAsyncPublic() => _ = CarregarFluxoAsync();

    private async Task CarregarFluxoAsync()
    {
        if (!_elevation.IsAdmin)
        {
            if (await _confirm.ConfirmAsync("Elevação necessária",
                    "Listar/remover drivers do Driver Store exige administrador.\nReabrir o app elevado?").ConfigureAwait(true))
            {
                if (!_elevation.RelaunchElevated("Escalada"))
                {
                    Log("Elevação cancelada (UAC). A escalada continua indisponível.");
                }
                return;
            }
            Log("Escalada cancelada: sem elevação.");
            return;
        }

        await ExecutarAsync("Escalada: listar drivers", async ct =>
        {
            var progress = new Progress<OperationEvent>(e => Log(OperationEventSink.Format(e)));
            var drivers = await _service.ListMediaDriversAsync(progress, ct).ConfigureAwait(true);

            Drivers.Clear();
            foreach (MediaDriverInfo d in drivers)
            {
                Drivers.Add(new MediaDriverViewModel(d));
            }
            OnPropertyChanged(nameof(TemDrivers));
            if (drivers.Count == 0) Log("Nada a remover: o Driver Store não tem drivers MEDIA de terceiros.");
        }).ConfigureAwait(true);
    }

    private async Task RemoverFluxoAsync()
    {
        if (!_elevation.IsAdmin)
        {
            Log("Remoção exige administrador. Reabra o app elevado.");
            return;
        }

        var selecionados = Drivers.Where(d => d.Selecionado).Select(d => d.Info).ToList();
        if (selecionados.Count == 0)
        {
            Log("Nenhum driver selecionado para remover.");
            return;
        }

        await ExecutarAsync("Escalada: remover drivers", async ct =>
        {
            string lista = string.Join("\n", selecionados.Select(d => $"• {d.OemInf} — {d.Provider}"));
            if (!await _confirm.ConfirmAsync("Remover drivers",
                    $"Desinstalar e apagar do store {selecionados.Count} driver(s)?\n\n{lista}\n\nO backup de drivers será conferido/gerado antes.").ConfigureAwait(true))
            {
                Log("Remoção cancelada pelo operador.");
                return;
            }

            var progress = new Progress<OperationEvent>(e => Log(OperationEventSink.Format(e)));

            string? pastaBackup = await _service.EnsureDriverBackupAsync(_paths.AppData, _run, progress, ct).ConfigureAwait(true);
            if (pastaBackup is null)
            {
                Log("Backup de drivers indisponível: remoção abortada.");
                return;
            }
            Log($"Backup de drivers OK: {pastaBackup}");

            var resultados = await _service.RemoveDriversAsync(selecionados, _run, _confirm, progress, ct).ConfigureAwait(true);
            int falhas = resultados.Count(r => !r.Ok);
            Log(falhas == 0
                ? $"Remoção concluída: {resultados.Count} driver(s) removido(s). Reinicie o PC e rode o Diagnóstico."
                : $"Remoção concluída com {falhas} falha(s). Reinicie o PC e rode o Diagnóstico.");

            // Recarrega a lista após as remoções.
            Drivers.Clear();
            var restantes = await _service.ListMediaDriversAsync(progress, ct).ConfigureAwait(true);
            foreach (MediaDriverInfo d in restantes)
            {
                Drivers.Add(new MediaDriverViewModel(d));
            }
            OnPropertyChanged(nameof(TemDrivers));
        }).ConfigureAwait(true);
    }
}
