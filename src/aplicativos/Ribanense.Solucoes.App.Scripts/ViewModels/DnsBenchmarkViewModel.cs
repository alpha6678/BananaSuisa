using System.Collections.ObjectModel;
using System.Net;
using System.Windows.Input;
using Ribanense.Solucoes.App.Scripts.Scripts.Commands;
using Ribanense.Solucoes.App.Scripts.Scripts.Dns;
using Ribanense.Solucoes.PluginSDK.Logging;
using Ribanense.Solucoes.UI;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Scripts.ViewModels;

/// <summary>
/// Conduz a UI da tela "Testar melhor DNS": seleção de servidores, execução
/// do benchmark com saída em tempo real (estilo terminal), ranking dos
/// resultados e aplicação opcional do DNS vencedor na interface de rede.
/// </summary>
public sealed class DnsBenchmarkViewModel : ObservableObject
{
    private readonly IDnsBenchmarkService _benchmarkService;
    private readonly INetworkDnsDetector _networkDetector;
    private readonly ICommandSequenceRunner _commandRunner;
    private readonly IAppJsonLog? _logger;
    private readonly string _activeInterfaceAlias;

    private CancellationTokenSource? _cts;
    private bool _isBusy;
    private string _customDnsInput = string.Empty;
    private string? _statusText = "Selecione os servidores e clique em Testar.";
    private DnsResultRowViewModel? _selectedResult;

    public DnsBenchmarkViewModel(
        IDnsBenchmarkService benchmarkService,
        INetworkDnsDetector networkDetector,
        ICommandSequenceRunner commandRunner,
        IAppJsonLog? logger = null)
    {
        _benchmarkService = benchmarkService ?? throw new ArgumentNullException(nameof(benchmarkService));
        _networkDetector = networkDetector ?? throw new ArgumentNullException(nameof(networkDetector));
        _commandRunner = commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));
        _logger = logger;

        Servers = new ObservableCollection<DnsServerSelectionViewModel>();
        Results = new ObservableCollection<DnsResultRowViewModel>();
        LogLines = new ObservableCollection<string>();

        foreach (var wellKnown in WellKnownDnsServers.Default)
        {
            Servers.Add(new DnsServerSelectionViewModel(wellKnown));
        }

        foreach (var detected in _networkDetector.DetectCurrent())
        {
            Servers.Add(new DnsServerSelectionViewModel(detected));
        }

        _activeInterfaceAlias = _networkDetector.DetectActiveInterfaceNames().FirstOrDefault() ?? string.Empty;

        AddCustomDnsCommand = new RelayCommand(AddCustomDns, () => IsValidCustomInput);
        RemoveServerCommand = new RelayCommand(p =>
        {
            if (p is DnsServerSelectionViewModel s) Servers.Remove(s);
        });
        RunCommand = new AsyncRelayCommand(RunAsync, () => !IsBusy && Servers.Any(s => s.IsSelected));
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy);
        ApplyDnsCommand = new AsyncRelayCommand(
            ApplyDnsAsync,
            () => !IsBusy && SelectedResult is { } r && r.Result.SuccessCount > 0 && !string.IsNullOrEmpty(_activeInterfaceAlias));
        CopyApplyCommandCommand = new RelayCommand(CopyApplyCommand, () => SelectedResult is not null);
    }

    public ObservableCollection<DnsServerSelectionViewModel> Servers { get; }
    public ObservableCollection<DnsResultRowViewModel> Results { get; }
    public ObservableCollection<string> LogLines { get; }

    public string CustomDnsInput
    {
        get => _customDnsInput;
        set
        {
            if (SetProperty(ref _customDnsInput, value))
            {
                OnPropertyChanged(nameof(IsValidCustomInput));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsValidCustomInput =>
        !string.IsNullOrWhiteSpace(_customDnsInput) && IPAddress.TryParse(_customDnsInput.Trim(), out _);

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string? StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public DnsResultRowViewModel? SelectedResult
    {
        get => _selectedResult;
        set
        {
            if (SetProperty(ref _selectedResult, value))
            {
                OnPropertyChanged(nameof(ApplyCommandPreview));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string ApplyCommandPreview =>
        SelectedResult is null || string.IsNullOrEmpty(_activeInterfaceAlias)
            ? string.Empty
            : DnsApplyCommandBuilder.Build(_activeInterfaceAlias, new[] { SelectedResult.IpAddress }).ToCommandText();

    public ICommand AddCustomDnsCommand { get; }
    public ICommand RemoveServerCommand { get; }
    public ICommand RunCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ApplyDnsCommand { get; }
    public ICommand CopyApplyCommandCommand { get; }

    private void AddCustomDns()
    {
        string ip = _customDnsInput.Trim();
        if (!IPAddress.TryParse(ip, out _)) return;

        if (Servers.Any(s => s.IpAddress == ip))
        {
            CustomDnsInput = string.Empty;
            return;
        }

        Servers.Add(new DnsServerSelectionViewModel(new DnsServerCandidate("Personalizado", ip, DnsServerOrigin.Personalizado)));
        CustomDnsInput = string.Empty;
    }

    private async Task RunAsync()
    {
        var selected = Servers.Where(s => s.IsSelected).Select(s => s.Candidate).ToList();
        if (selected.Count == 0)
        {
            StatusText = "Selecione ao menos um servidor DNS.";
            return;
        }

        IsBusy = true;
        Results.Clear();
        SelectedResult = null;
        StatusText = "Testando servidores DNS...";
        Log($"Iniciando teste de {selected.Count} servidor(es) DNS...");

        _cts = new CancellationTokenSource();
        try
        {
            var progress = new Progress<string>(Log);
            var ranked = await _benchmarkService
                .RunAsync(selected, DnsBenchmarkOptions.Default, progress, _cts.Token)
                .ConfigureAwait(true);

            int rank = 1;
            foreach (var result in ranked)
            {
                Results.Add(new DnsResultRowViewModel(rank++, result));
            }

            SelectedResult = Results.FirstOrDefault(r => r.IsWinner);
            StatusText = SelectedResult is not null
                ? $"Melhor DNS: {SelectedResult.Label} ({SelectedResult.IpAddress}), média {SelectedResult.AverageDisplay}."
                : "Nenhum servidor respondeu com sucesso.";
            Log(StatusText);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Teste cancelado.";
            Log(StatusText);
        }
        catch (Exception ex)
        {
            StatusText = $"Erro ao testar DNS: {ex.Message}";
            Log(StatusText);
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void CopyApplyCommand()
    {
        if (string.IsNullOrEmpty(ApplyCommandPreview)) return;
        LogLinesClipboard.CopyOrWarn(new[] { ApplyCommandPreview }, "Scripts");
    }

    private async Task ApplyDnsAsync()
    {
        if (SelectedResult is null || string.IsNullOrEmpty(_activeInterfaceAlias)) return;

        IsBusy = true;
        var step = DnsApplyCommandBuilder.Build(_activeInterfaceAlias, new[] { SelectedResult.IpAddress });
        Log($"Aplicando DNS {SelectedResult.IpAddress} na interface \"{_activeInterfaceAlias}\" (confirme o prompt do UAC)...");

        try
        {
            var progress = new Progress<string>(Log);
            var results = await _commandRunner
                .RunSequenceAsync(new[] { step }, progress, CancellationToken.None)
                .ConfigureAwait(true);

            var outcome = results.FirstOrDefault();
            StatusText = outcome is { Succeeded: true }
                ? "DNS aplicado com sucesso."
                : "Não foi possível confirmar a aplicação automática. Use o comando acima para aplicar manualmente.";
            Log(StatusText);
        }
        catch (Exception ex)
        {
            StatusText = $"Erro ao aplicar DNS: {ex.Message}";
            Log(StatusText);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Log(string line)
    {
        if (string.IsNullOrEmpty(line)) return;

        try { _logger?.Write(AppLogLevel.Information, "dns-benchmark", line); } catch { }

        if (System.Windows.Threading.Dispatcher.CurrentDispatcher.CheckAccess())
        {
            LogLines.Add(line);
        }
        else
        {
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(() => LogLines.Add(line));
        }
    }
}
