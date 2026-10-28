using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using Ribanense.Solucoes.App.Recuperacao.Recovery.Audio;
using Ribanense.Solucoes.App.Recuperacao.Recovery.Core;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Recuperacao.ViewModels;

/// <summary>Fase 5: instalar driver do codec a partir de pasta local com .inf.</summary>
public sealed class AudioCodecViewModel : FaseViewModelBase
{
    private readonly IAudioCodecInstallService _service;
    private CodecInfInfo? _infSelecionado;
    private string _pasta = string.Empty;

    public AudioCodecViewModel(BusyGate gate, Action<string> log, IAudioCodecInstallService service)
        : base(gate, log)
    {
        _service = service;
        ProcurarPastaCommand = new RelayCommand(ProcurarPasta);
        ProcurarInfCommand = new AsyncRelayCommand(() => ProcurarInfsAsync(), () => !gate.IsBusy);
        InstalarCommand = new AsyncRelayCommand(() => InstalarFluxoAsync(), () => !gate.IsBusy && InfSelecionado is not null);
    }

    public ICommand ProcurarPastaCommand { get; }

    public ICommand ProcurarInfCommand { get; }

    public ICommand InstalarCommand { get; }

    public string Pasta
    {
        get => _pasta;
        set
        {
            if (SetProperty(ref _pasta, value))
            {
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public ObservableCollection<CodecInfInfo> Infs { get; } = [];

    public CodecInfInfo? InfSelecionado
    {
        get => _infSelecionado;
        set
        {
            if (SetProperty(ref _infSelecionado, value))
            {
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    private void ProcurarPasta()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Pasta com o driver do codec (ex.: C:\\Drivers\\ESS)",
        };
        bool? ok = dialog.ShowDialog();
        if (ok == true)
        {
            Pasta = dialog.FolderName;
        }
    }

    private async Task ProcurarInfsAsync()
    {
        await ExecutarAsync("Procurar .inf", async ct =>
        {
            Infs.Clear();
            InfSelecionado = null;
            var infs = await _service.ScanInfsAsync(Pasta, ct).ConfigureAwait(true);
            foreach (CodecInfInfo inf in infs)
            {
                Infs.Add(inf);
            }
            if (infs.Count == 0)
            {
                Log($"Nenhum .inf encontrado em {Pasta} (descompacte o pacote do fabricante nessa pasta).");
            }
            else if (infs.Count == 1)
            {
                InfSelecionado = infs[0];
            }
        }).ConfigureAwait(true);
    }

    private async Task InstalarFluxoAsync()
    {
        if (InfSelecionado is null)
        {
            Log("Selecione o .inf do codec de áudio.");
            return;
        }

        await ExecutarAsync("Instalar codec", async ct =>
        {
            var progress = new Progress<OperationEvent>(e =>
            {
                Log(OperationEventSink.Format(e));
                if (e.Kind == OperationEventKind.Info) Progresso = e.Message;
            });

            CodecInstallResultado resultado = await _service
                .InstallAsync(InfSelecionado.Caminho, progress, ct).ConfigureAwait(true);
            _ = resultado;
        }).ConfigureAwait(true);
    }
}
