using System.Collections.ObjectModel;
using System.Windows.Input;
using Ribanense.Solucoes.App.Chocolatey.Services.Diagnostics;
using Ribanense.Solucoes.PluginSDK.Logging;
using Ribanense.Solucoes.UI;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Chocolatey.ViewModels;

public sealed class ModuleViewModel : ObservableObject
{
    private readonly IChocolateyDiagnostics _diag;
    private readonly IAppJsonLog _log;

    public ModuleViewModel(IChocolateyDiagnostics diag, IAppJsonLog log)
    {
        _diag = diag;
        _log = log;
        InspectCommand = new AsyncRelayCommand(_ => InspectAsync(), _ => !IsBusy);
        CopyLogCommand = new RelayCommand(() => LogLinesClipboard.CopyOrWarn(LogLines, "Gestor Chocolatey"));
    }

    public ObservableCollection<string> LogLines { get; } = new();

    private ChocolateyStatus? _status;
    public ChocolateyStatus? Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(HealthyLabel));
                OnPropertyChanged(nameof(ChocolateyLine));
                OnPropertyChanged(nameof(AdminLine));
            }
        }
    }

    public string HealthyLabel => Status switch
    {
        null => string.Empty,
        { Healthy: true } => "Chocolatey localizado e respondendo.",
        _ => "Chocolatey indisponivel ou com problema."
    };

    public string ChocolateyLine =>
        Status is { Found: true } s
            ? $"choco {s.Version ?? "(versao desconhecida)"} - {s.Path}"
            : Status?.Error ?? "choco.exe nao encontrado.";

    public string AdminLine =>
        "Instalar, atualizar e remover pacotes pode exigir execução como administrador, dependendo do pacote e da configuração do Chocolatey.";

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

    private string? _statusMessage;
    public string? StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand InspectCommand { get; }
    public ICommand CopyLogCommand { get; }

    public async Task InspectAsync()
    {
        IsBusy = true;
        StatusMessage = "Inspecionando Chocolatey...";
        AppendLog("== Verificacao do Chocolatey ==");
        try
        {
            var s = await _diag.InspectAsync(CancellationToken.None).ConfigureAwait(true);
            Status = s;
            StatusMessage = s.Healthy
                ? "Chocolatey saudavel."
                : "Chocolatey nao esta pronto.";
            AppendLog(s.Healthy ? $"[OK] choco {s.Version}" : $"[!!] {s.Error}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro: {ex.Message}";
            _log.Write(AppLogLevel.Error, "module.inspect", "Falha ao inspecionar Chocolatey.", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AppendLog(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        LogLines.Add(line);
        while (LogLines.Count > 400) LogLines.RemoveAt(0);
    }
}
