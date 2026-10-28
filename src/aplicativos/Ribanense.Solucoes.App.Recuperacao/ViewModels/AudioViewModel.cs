using System;
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
/// Módulo Áudio: reúne as cinco fases do reparo (diagnóstico, backup, reparo
/// seguro, escalada e instalar codec), com trava única de operação e elevação.
/// </summary>
public sealed class AudioViewModel : ObservableObject
{
    private readonly IElevationService _elevation;
    private readonly Action<string> _log;

    public AudioViewModel(
        AppPaths paths,
        Action<string> log,
        IConfirmDialog confirm,
        IElevationService elevation)
    {
        _elevation = elevation;
        _log = log;

        BusyGate gate = new();
        var runner = new ProcessRunner();

        RunContext = RunContext.CreateNew(paths.AppData);

        Diagnostico = new AudioDiagnosticoViewModel(gate, log, new AudioDiagnosticsService(runner), RunContext);
        Backup = new AudioBackupViewModel(gate, log, new AudioBackupService(runner), RunContext, confirm);
        Reparo = new AudioReparoViewModel(gate, log, new AudioRepairService(runner), RunContext, confirm);
        Escalada = new AudioEscaladaViewModel(gate, log, new AudioEscalationService(runner), RunContext, paths, confirm, elevation);
        Codec = new AudioCodecViewModel(gate, log, new AudioCodecInstallService(runner));

        Fases =
        [
            new ModuleTabViewModel("Diagnóstico", Diagnostico) { IsSelected = true },
            new ModuleTabViewModel("Backup", Backup),
            new ModuleTabViewModel("Reparo", Reparo),
            new ModuleTabViewModel("Escalada", Escalada),
            new ModuleTabViewModel("Instalar codec", Codec),
        ];
        foreach (ModuleTabViewModel fase in Fases)
        {
            fase.PropertyChanged += OnFasePropertyChanged;
        }

        ReabrirElevadoCommand = new RelayCommand(
            () =>
            {
                if (!_elevation.RelaunchElevated())
                {
                    log("Elevação cancelada (UAC). O diagnóstico continua funcionando; os reparos exigem admin.");
                }
            },
            () => !_elevation.IsAdmin);
    }

    public RunContext RunContext { get; }

    public string PastaExecucao => RunContext.Root;

    public ObservableCollection<ModuleTabViewModel> Fases { get; }

    public ModuleTabViewModel? FaseSelecionada => Fases.FirstOrDefault(f => f.IsSelected);

    public AudioDiagnosticoViewModel Diagnostico { get; }
    public AudioBackupViewModel Backup { get; }
    public AudioReparoViewModel Reparo { get; }
    public AudioEscaladaViewModel Escalada { get; }
    public AudioCodecViewModel Codec { get; }

    public bool NaoElevado => !_elevation.IsAdmin;

    public ICommand ReabrirElevadoCommand { get; }

    public void RefreshElevation()
    {
        OnPropertyChanged(nameof(NaoElevado));
    }

    /// <summary>
    /// Auto-execução de fase (usada quando o app é relançado elevado com --run).
    /// Valores aceitos: Diagnostico, Backup, Reparo, Escalada, Codec.
    /// </summary>
    public void RequestAutoStart(string fase)
    {
        ModuleTabViewModel? alvo = fase.Trim().ToLowerInvariant() switch
        {
            "diagnostico" => Fases[0],
            "backup" => Fases[1],
            "reparo" => Fases[2],
            "escalada" => Fases[3],
            "codec" => Fases[4],
            _ => null,
        };
        if (alvo is null)
        {
            _log($"Fase de auto-execução desconhecida: '{fase}'.");
            return;
        }

        foreach (ModuleTabViewModel f in Fases.Where(f => !ReferenceEquals(f, alvo)))
        {
            f.IsSelected = false;
        }
        alvo.IsSelected = true;
        OnPropertyChanged(nameof(FaseSelecionada));

        _log($"Executando fase '{alvo.Title}' (auto-start)...");
        switch (fase.Trim().ToLowerInvariant())
        {
            case "diagnostico": Diagnostico.ExecutarAsyncPublic(); break;
            case "backup": Backup.ExecutarAsyncPublic(); break;
            case "reparo": Reparo.ExecutarAsyncPublic(); break;
            case "escalada": Escalada.CarregarDriversAsyncPublic(); break;
        }
    }

    private void OnFasePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ModuleTabViewModel.IsSelected)) return;
        if (sender is not ModuleTabViewModel { IsSelected: true } selecionada) return;

        foreach (ModuleTabViewModel fase in Fases.Where(f => !ReferenceEquals(f, selecionada)))
        {
            fase.IsSelected = false;
        }
        OnPropertyChanged(nameof(FaseSelecionada));
    }
}
