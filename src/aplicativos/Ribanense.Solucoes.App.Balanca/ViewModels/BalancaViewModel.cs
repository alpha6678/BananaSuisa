using System.Collections.ObjectModel;
using System.Globalization;
using System.IO.Ports;
using System.Windows;
using System.Windows.Input;
using Ribanense.Solucoes.App.Balanca.Domain;
using Ribanense.Solucoes.App.Balanca.Serial;
using Ribanense.Solucoes.App.Balanca.Services;
using Ribanense.Solucoes.PluginSDK.Logging;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Balanca.ViewModels;

/// <summary>
/// ViewModel da tela de teste de balança, organizada como um roteiro de três passos:
/// escolher a porta no inventário do checkout, identificar o modelo (que traz a
/// configuração documentada) e testar a leitura. A varredura de combinações é o plano B,
/// oferecido depois que a configuração sugerida não responde.
/// </summary>
public sealed class BalancaViewModel : ObservableObject, IDisposable
{
    private readonly RealSerialChannelFactory _realFactory = new();
    private readonly ProfileStore _profiles;
    private readonly IAppJsonLog? _logger;
    private readonly SerialPortWatcher _portWatcher = new();
    private Action<string>? _logSink;

    private BalancaReader? _reader;
    private CancellationTokenSource? _monitorCts;
    private CancellationTokenSource? _scanCts;
    private string? _activePort;

    private IReadOnlyList<SerialConfig> _stepCandidates = Array.Empty<SerialConfig>();
    private int _stepIndex = -1;

    public BalancaViewModel(ProfileStore profiles, IAppJsonLog? logger = null)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _logger = logger;

        Models = BalancaModelRegistry.All;
        _selectedModel = BalancaModelRegistry.Default;

        RefreshPortsCommand = new RelayCommand(RefreshPorts);
        ActivateCommand = new AsyncRelayCommand(_ => ActivateAsync(), _ => CanActivate);
        DeactivateCommand = new RelayCommand(Deactivate, () => IsActive);
        ReadWeightCommand = new AsyncRelayCommand(_ => ReadOnceAsync(), _ => IsActive && !IsMonitoring);
        ToggleMonitorCommand = new RelayCommand(ToggleMonitor, () => IsActive);
        ClearCommand = new RelayCommand(ClearReadout);
        UseSuggestedConfigCommand = new RelayCommand(ApplySuggestedConfig, () => !IsActive && !IsScanning);

        StartScanCommand = new AsyncRelayCommand(_ => StartFullScanAsync(), _ => CanStartScan);
        StopScanCommand = new RelayCommand(StopScan, () => IsScanning || IsStepping);
        StartStepCommand = new AsyncRelayCommand(_ => StartStepScanAsync(), _ => CanStartScan);
        NextStepCommand = new AsyncRelayCommand(_ => NextStepAsync(), _ => IsStepping && !IsBusy);
        UseCurrentConfigCommand = new RelayCommand(UseCurrentCandidate, () => CurrentCandidate is not null);
        UseResultCommand = new RelayCommand(p => UseResult(p as ScanResult), p => p is ScanResult);

        RefreshPorts();
        LoadProfileForModel(SelectedModel);

        _portWatcher.PortsChanged += OnPortsChanged;
        _portWatcher.Start();
    }

    private void OnPortsChanged(IReadOnlyList<SerialPortInfo> present)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => OnPortsChanged(present));
            return;
        }

        // Modelo simulado não usa portas reais; ignore hot-plug.
        if (SelectedModel.IsSimulated) return;

        bool activePortGone = IsActive && SelectedPort is not null
            && !present.Any(p => string.Equals(p.Port, SelectedPort.Port, StringComparison.OrdinalIgnoreCase));

        if (activePortGone)
        {
            Log($"Porta {SelectedPort!.Port} removida; encerrando a sessão.");
            Deactivate();
        }

        // Durante varredura completa não mexemos na lista para não afetar os candidatos.
        if (!IsScanning) RefreshPorts();
    }

    #region Catálogos / opções

    public IReadOnlyList<BalancaModel> Models { get; }
    public ObservableCollection<SerialPortInfo> Ports { get; } = new();

    public IReadOnlyList<int> BaudRateOptions { get; } =
        new[] { 110, 300, 600, 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200 };

    public IReadOnlyList<int> DataBitsOptions { get; } = new[] { 5, 6, 7, 8 };
    public IReadOnlyList<Parity> ParityOptions { get; } = Enum.GetValues<Parity>();
    public IReadOnlyList<StopBits> StopBitsOptions { get; } =
        new[] { StopBits.One, StopBits.OnePointFive, StopBits.Two };
    public IReadOnlyList<Handshake> HandshakeOptions { get; } = Enum.GetValues<Handshake>();

    #endregion

    #region Seleção de modelo/porta e configuração

    private BalancaModel _selectedModel;
    public BalancaModel SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (SetProperty(ref _selectedModel, value) && value is not null)
            {
                StopScan();
                Deactivate();
                RefreshPorts();
                LoadProfileForModel(value);
                OnPropertyChanged(nameof(ModelNotes));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>Resumo do protocolo do modelo selecionado, exibido no passo 2.</summary>
    public string ModelNotes => SelectedModel.Notes;

    private SerialPortInfo? _selectedPort;
    public SerialPortInfo? SelectedPort
    {
        get => _selectedPort;
        set
        {
            if (SetProperty(ref _selectedPort, value))
            {
                OnPropertyChanged(nameof(SelectedPortHint));
                UpdateSuggestedConfigText();
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>Orientação sobre a porta escolhida (origem e se está ocupada).</summary>
    public string SelectedPortHint => SelectedPort switch
    {
        null => "Nenhuma porta selecionada.",
        { IsBusy: true } p => $"{p.RoleHint}. Está em uso por outro programa — feche o outro sistema antes de ativar.",
        { IsBluetooth: true } p => $"{p.RoleHint}. Se a balança é ligada por cabo, provavelmente não é esta porta.",
        var p => p.RoleHint + ".",
    };

    private string _portsSummary = "";
    public string PortsSummary { get => _portsSummary; private set => SetProperty(ref _portsSummary, value); }

    private int _baudRate = 9600;
    public int BaudRate
    {
        get => _baudRate;
        set { if (SetProperty(ref _baudRate, value)) UpdateSuggestedConfigText(); }
    }

    private int _dataBits = 8;
    public int DataBits
    {
        get => _dataBits;
        set { if (SetProperty(ref _dataBits, value)) UpdateSuggestedConfigText(); }
    }

    private Parity _parity = Parity.None;
    public Parity Parity
    {
        get => _parity;
        set { if (SetProperty(ref _parity, value)) UpdateSuggestedConfigText(); }
    }

    private StopBits _stopBits = StopBits.One;
    public StopBits StopBits
    {
        get => _stopBits;
        set { if (SetProperty(ref _stopBits, value)) UpdateSuggestedConfigText(); }
    }

    private Handshake _handshake = Handshake.None;
    public Handshake Handshake
    {
        get => _handshake;
        set { if (SetProperty(ref _handshake, value)) UpdateSuggestedConfigText(); }
    }

    private int _timeoutMs = 2000;
    public int TimeoutMs { get => _timeoutMs; set => SetProperty(ref _timeoutMs, value); }

    private string _currentConfigText = "";
    /// <summary>Configuração que será usada ao ativar, ex.: "COM5 9600 8N1".</summary>
    public string CurrentConfigText { get => _currentConfigText; private set => SetProperty(ref _currentConfigText, value); }

    private bool _showAdvanced;
    /// <summary>Abre os parâmetros seriais e a varredura ampla (casos difíceis).</summary>
    public bool ShowAdvanced { get => _showAdvanced; set => SetProperty(ref _showAdvanced, value); }

    private bool _deepScan;
    public bool DeepScan { get => _deepScan; set => SetProperty(ref _deepScan, value); }

    private bool _scanAllPorts;
    /// <summary>Por padrão a varredura fica restrita à porta escolhida no passo 1.</summary>
    public bool ScanAllPorts { get => _scanAllPorts; set => SetProperty(ref _scanAllPorts, value); }

    #endregion

    #region Estado / leitura

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        private set { if (SetProperty(ref _isActive, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    private bool _isMonitoring;
    public bool IsMonitoring
    {
        get => _isMonitoring;
        private set
        {
            if (SetProperty(ref _isMonitoring, value))
            {
                OnPropertyChanged(nameof(MonitorButtonText));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string MonitorButtonText => IsMonitoring ? "Parar monitor" : "Monitorar balança";

    private bool _isScanning;
    public bool IsScanning
    {
        get => _isScanning;
        private set { if (SetProperty(ref _isScanning, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set { if (SetProperty(ref _isBusy, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    private string _weightDisplay = "----";
    public string WeightDisplay { get => _weightDisplay; private set => SetProperty(ref _weightDisplay, value); }

    private string _statusText = "Aguardando";
    public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

    private string _lastResponseAscii = "";
    public string LastResponseAscii { get => _lastResponseAscii; private set => SetProperty(ref _lastResponseAscii, value); }

    private string _lastResponseHex = "";
    public string LastResponseHex { get => _lastResponseHex; private set => SetProperty(ref _lastResponseHex, value); }

    private string _scanProgress = "";
    public string ScanProgress { get => _scanProgress; private set => SetProperty(ref _scanProgress, value); }

    private string _advice = "";
    /// <summary>Próximo passo sugerido depois de uma falha, exibido no passo 3.</summary>
    public string Advice
    {
        get => _advice;
        private set { if (SetProperty(ref _advice, value)) OnPropertyChanged(nameof(HasAdvice)); }
    }

    public bool HasAdvice => !string.IsNullOrWhiteSpace(Advice);

    public ObservableCollection<ScanResult> ScanResults { get; } = new();

    private bool _hasScanResults;
    public bool HasScanResults { get => _hasScanResults; private set => SetProperty(ref _hasScanResults, value); }

    private SerialConfig? _currentCandidate;
    public SerialConfig? CurrentCandidate
    {
        get => _currentCandidate;
        private set
        {
            if (SetProperty(ref _currentCandidate, value))
            {
                OnPropertyChanged(nameof(CurrentCandidateText));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string CurrentCandidateText => CurrentCandidate?.ShortDescription ?? "—";

    private string _stepProgress = "";
    public string StepProgress { get => _stepProgress; private set => SetProperty(ref _stepProgress, value); }

    public bool IsStepping => _stepIndex >= 0 && _stepIndex < _stepCandidates.Count;

    #endregion

    #region Comandos

    public ICommand RefreshPortsCommand { get; }
    public ICommand ActivateCommand { get; }
    public ICommand DeactivateCommand { get; }
    public ICommand ReadWeightCommand { get; }
    public ICommand ToggleMonitorCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand UseSuggestedConfigCommand { get; }
    public ICommand StartScanCommand { get; }
    public ICommand StopScanCommand { get; }
    public ICommand StartStepCommand { get; }
    public ICommand NextStepCommand { get; }
    public ICommand UseCurrentConfigCommand { get; }
    public ICommand UseResultCommand { get; }

    private bool CanActivate => !IsActive && !IsScanning && SelectedPort is not null;
    private bool CanStartScan => !IsScanning && !IsActive && Ports.Count > 0;

    #endregion

    public void AttachUiLog(Action<string> logSink) => _logSink = logSink;

    #region Modelo/portas

    private ISerialChannelFactory FactoryFor(BalancaModel model) =>
        model.IsSimulated
            ? new SimulatedSerialChannelFactory(SerialConfig.Default(SimulatedSerialChannelFactory.SimulatedPort))
            : _realFactory;

    private void RefreshPorts()
    {
        var previous = SelectedPort?.Port;
        Ports.Clear();
        foreach (var p in FactoryFor(SelectedModel).ListPorts())
        {
            // A porta que nós mesmos mantemos aberta não é "ocupada por outro programa".
            Ports.Add(string.Equals(p.Port, _activePort, StringComparison.OrdinalIgnoreCase)
                ? p with { IsBusy = false }
                : p);
        }

        SelectedPort = Ports.FirstOrDefault(p => string.Equals(p.Port, previous, StringComparison.OrdinalIgnoreCase))
                       ?? PickLikelyScalePort();

        UpdatePortsSummary();

        if (Ports.Count == 0)
            Log("Nenhuma porta serial encontrada. Conecte a balança (COM/USB-serial) e clique em Atualizar.");
    }

    /// <summary>
    /// Primeira sugestão de porta: descarta as de link Bluetooth (tipicamente a
    /// maquininha TEF no caixa) e as já ocupadas por outro programa.
    /// </summary>
    private SerialPortInfo? PickLikelyScalePort() =>
        Ports.FirstOrDefault(p => !p.IsBluetooth && !p.IsBusy)
        ?? Ports.FirstOrDefault(p => !p.IsBusy)
        ?? Ports.FirstOrDefault();

    private void UpdatePortsSummary()
    {
        if (Ports.Count == 0)
        {
            PortsSummary = "Nenhuma porta COM presente neste computador.";
            return;
        }

        int bluetooth = Ports.Count(p => p.IsBluetooth);
        int busy = Ports.Count(p => p.IsBusy);

        var parts = new List<string> { $"{Ports.Count} porta(s) COM presente(s)" };
        if (bluetooth > 0) parts.Add($"{bluetooth} de link Bluetooth (normalmente TEF)");
        if (busy > 0) parts.Add($"{busy} em uso por outro programa");

        PortsSummary = string.Join(" · ", parts) + ".";
    }

    private void LoadProfileForModel(BalancaModel model)
    {
        var saved = _profiles.TryLoad(model.Key);
        var basis = saved ?? SuggestedConfigFor(model);
        ApplyConfig(basis);
        UpdateSuggestedConfigText();
        Log(saved is not null
            ? $"Perfil salvo carregado para {model.DisplayName}: {saved.ShortDescription}."
            : $"Configuração documentada de {model.DisplayName}: {basis.ShortDescription}.");
    }

    private SerialConfig SuggestedConfigFor(BalancaModel model) =>
        model.DefaultConfig(SelectedPort?.Port ?? Ports.FirstOrDefault()?.Port ?? "COM1");

    private void ApplySuggestedConfig()
    {
        var suggested = SuggestedConfigFor(SelectedModel);
        ApplyConfig(suggested);
        UpdateSuggestedConfigText();
        Advice = "";
        Log($"Configuração documentada restaurada: {suggested.ShortDescription}.");
    }

    private void ApplyConfig(SerialConfig cfg)
    {
        BaudRate = cfg.BaudRate;
        DataBits = cfg.DataBits;
        Parity = cfg.Parity;
        StopBits = cfg.StopBits;
        Handshake = cfg.Handshake;
        if (cfg.TimeoutMs > 0) TimeoutMs = cfg.TimeoutMs;

        var match = Ports.FirstOrDefault(p => string.Equals(p.Port, cfg.Port, StringComparison.OrdinalIgnoreCase));
        if (match is not null) SelectedPort = match;

        UpdateSuggestedConfigText();
    }

    private void UpdateSuggestedConfigText() => CurrentConfigText = BuildConfig().ShortDescription;

    private SerialConfig BuildConfig() =>
        new(SelectedPort?.Port ?? "COM1", BaudRate, DataBits, Parity, StopBits, Handshake, TimeoutMs);

    #endregion

    #region Teste na porta escolhida

    private async Task ActivateAsync()
    {
        if (SelectedPort is null) { Log("Selecione uma porta serial."); return; }

        if (SelectedPort.IsBusy)
            Log($"{SelectedPort.Port} aparece como em uso por outro programa; a ativação pode falhar.");

        try
        {
            IsBusy = true;
            var config = BuildConfig();
            _reader = new BalancaReader(FactoryFor(SelectedModel));
            await Task.Run(() => _reader.Activate(config, SelectedModel.Protocol)).ConfigureAwait(true);
            IsActive = true;
            _activePort = config.Port;
            Advice = "";
            Log($"Balança ativada: {SelectedModel.DisplayName} em {config.ShortDescription}.");
        }
        catch (Exception ex)
        {
            _reader?.Dispose();
            _reader = null;
            IsActive = false;
            _activePort = null;
            Advice = $"Não foi possível abrir {SelectedPort.Port}. Confira se a porta é mesmo a da balança " +
                     "(a de link Bluetooth costuma ser a maquininha TEF) e se outro sistema não está usando-a.";
            Log($"Falha ao ativar: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Deactivate()
    {
        StopMonitor();
        if (_reader is not null)
        {
            _reader.Dispose();
            _reader = null;
            Log("Balança desativada.");
        }
        IsActive = false;
        _activePort = null;
    }

    private async Task ReadOnceAsync()
    {
        if (_reader is not { IsActive: true }) return;
        try
        {
            IsBusy = true;
            var outcome = await _reader.ReadWeightAsync().ConfigureAwait(true);
            ShowReading(outcome.Reading);
            LogOutcome(outcome);
            UpdateAdviceFor(outcome);
        }
        catch (Exception ex)
        {
            Log($"Erro na leitura: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void UpdateAdviceFor(SerialReadOutcome outcome)
    {
        if (outcome.Reading.HasResponse)
        {
            Advice = "";
            return;
        }

        string port = SelectedPort?.Port ?? "a porta";
        Advice = $"A balança não respondeu em {CurrentConfigText}. Use \"Tentar outras configurações\" " +
                 $"para testar as combinações conhecidas de {SelectedModel.DisplayName} em {port}.";
    }

    private void ToggleMonitor()
    {
        if (IsMonitoring) StopMonitor();
        else StartMonitor();
    }

    private void StartMonitor()
    {
        if (_reader is not { IsActive: true }) return;
        _monitorCts = new CancellationTokenSource();
        IsMonitoring = true;
        Log("Monitor contínuo iniciado.");
        _ = MonitorLoopAsync(_monitorCts.Token);
    }

    private void StopMonitor()
    {
        if (_monitorCts is null) return;
        _monitorCts.Cancel();
        _monitorCts.Dispose();
        _monitorCts = null;
        IsMonitoring = false;
        Log("Monitor contínuo parado.");
    }

    private async Task MonitorLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _reader is { IsActive: true })
            {
                var outcome = await _reader.ReadWeightAsync(ct).ConfigureAwait(true);
                ShowReading(outcome.Reading);
                await Task.Delay(150, ct).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log($"Monitor interrompido: {ex.Message}");
            StopMonitor();
        }
    }

    private void ClearReadout()
    {
        WeightDisplay = "----";
        StatusText = "Aguardando";
        LastResponseAscii = "";
        LastResponseHex = "";
        Advice = "";
    }

    #endregion

    #region Varredura de apoio

    /// <summary>
    /// Portas a varrer. O padrão é apenas a porta escolhida no passo 1; abrir para todas
    /// só acontece quando o usuário pede explicitamente no painel avançado.
    /// </summary>
    private IReadOnlyList<string> GetPortsToScan()
    {
        if (!ScanAllPorts && SelectedPort is not null)
            return new[] { SelectedPort.Port };

        return Ports.Select(p => p.Port).ToList();
    }

    private async Task StartFullScanAsync()
    {
        var ports = GetPortsToScan();
        if (ports.Count == 0) { Log("Sem portas para varrer."); return; }

        var model = SelectedModel;
        var options = new ScanOptions { Deep = DeepScan, TimeoutMsPerAttempt = Math.Max(400, Math.Min(TimeoutMs, 3000)) };
        var engine = new ScanEngine(FactoryFor(model));
        var candidates = engine.BuildCandidates(model, ports, options);

        ScanResults.Clear();
        HasScanResults = false;
        IsScanning = true;
        _scanCts = new CancellationTokenSource();
        int total = candidates.Count;
        int done = 0;
        Log($"Testando outras configurações de {model.DisplayName}: {total} combinações em {ports.Count} porta(s). " +
            $"Estimativa máxima: {EstimateDuration(candidates, options.TimeoutMsPerAttempt)}.");

        var progress = new Progress<ScanResult>(r =>
        {
            done++;
            ScanProgress = $"Testando {r.Config.ShortDescription}  ({done}/{total})";
            if (r.Reading.HasResponse)
            {
                ScanResults.Add(r);
                HasScanResults = true;
                Log($"[hit] {r.Config.ShortDescription} → {FormatReading(r.Reading)}");
            }
        });

        try
        {
            var hits = await engine.ScanAllAsync(model, ports, options, progress, _scanCts.Token);
            ScanResults.Clear();
            foreach (var h in hits) ScanResults.Add(h);
            HasScanResults = ScanResults.Count > 0;
            ScanProgress = $"Concluído: {hits.Count} combinação(ões) com resposta de {total} testadas.";
            Log(ScanProgress);
            if (hits.Count > 0)
            {
                ShowReading(hits[0].Reading);
                Advice = "Escolha a linha que trouxe o peso correto e clique em \"Usar\" para salvar essa configuração.";
            }
            else
            {
                Advice = ScanAllPorts
                    ? "Nenhuma combinação respondeu. Confira cabo, alimentação da balança e o modelo selecionado."
                    : "Nenhuma combinação respondeu nesta porta. Tente outra porta da lista ou marque \"Varrer todas as portas\" no painel avançado.";
            }
        }
        catch (OperationCanceledException)
        {
            ScanProgress = $"Varredura cancelada ({done}/{total}).";
            Log(ScanProgress);
        }
        catch (Exception ex)
        {
            Log($"Erro na varredura: {ex.Message}");
        }
        finally
        {
            _scanCts?.Dispose();
            _scanCts = null;
            IsScanning = false;
        }
    }

    private void StopScan()
    {
        _scanCts?.Cancel();

        bool wasStepping = _stepCandidates.Count > 0;
        _stepCandidates = Array.Empty<SerialConfig>();
        _stepIndex = -1;
        CurrentCandidate = null;
        StepProgress = "";

        // A varredura completa dispõe o CTS no próprio finally; aqui só liberamos o do
        // modo passo a passo (quando não há varredura completa em andamento).
        if (!IsScanning)
        {
            _scanCts?.Dispose();
            _scanCts = null;
        }

        if (wasStepping) OnPropertyChanged(nameof(IsStepping));
        CommandManager.InvalidateRequerySuggested();
    }

    private async Task StartStepScanAsync()
    {
        var ports = GetPortsToScan();
        if (ports.Count == 0) { Log("Sem portas para varrer."); return; }

        var options = new ScanOptions { Deep = DeepScan, TimeoutMsPerAttempt = Math.Max(400, Math.Min(TimeoutMs, 3000)) };
        var engine = new ScanEngine(FactoryFor(SelectedModel));
        _stepCandidates = engine.BuildCandidates(SelectedModel, ports, options);
        _stepIndex = -1;
        ScanResults.Clear();
        HasScanResults = false;
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        OnPropertyChanged(nameof(IsStepping));
        Log($"Varredura passo a passo iniciada: {_stepCandidates.Count} combinações. " +
            $"Estimativa máxima: {EstimateDuration(_stepCandidates, options.TimeoutMsPerAttempt)}.");
        await NextStepAsync().ConfigureAwait(true);
    }

    private async Task NextStepAsync()
    {
        if (_stepCandidates.Count == 0) return;
        _stepIndex++;
        if (_stepIndex >= _stepCandidates.Count)
        {
            Log("Fim das combinações da varredura passo a passo.");
            StopScan();
            return;
        }

        OnPropertyChanged(nameof(IsStepping));
        var config = _stepCandidates[_stepIndex];
        CurrentCandidate = config;
        StepProgress = $"{_stepIndex + 1} / {_stepCandidates.Count}";

        try
        {
            IsBusy = true;
            ScanProgress = $"Testando {config.ShortDescription}...";
            var engine = new ScanEngine(FactoryFor(SelectedModel));
            var token = _scanCts?.Token ?? CancellationToken.None;
            var result = await engine.ProbeAsync(SelectedModel, config, token).ConfigureAwait(true);
            ShowReading(result.Reading);
            if (result.Reading.HasResponse)
            {
                if (!ScanResults.Any(r => r.Config.ShortDescription == result.Config.ShortDescription))
                    ScanResults.Add(result);
                HasScanResults = ScanResults.Count > 0;
                Log($"[{StepProgress}] {config.ShortDescription} → {FormatReading(result.Reading)}");
            }
            else
            {
                string detail = result.Error is null ? "sem resposta" : result.Error;
                Log($"[{StepProgress}] {config.ShortDescription} → {detail}");
            }
        }
        catch (OperationCanceledException)
        {
            Log($"Teste cancelado em {config.ShortDescription}.");
        }
        catch (Exception ex)
        {
            Log($"Erro ao testar {config.ShortDescription}: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void UseCurrentCandidate()
    {
        if (CurrentCandidate is null) return;
        ApplyAndSave(CurrentCandidate);
        StopScan();
    }

    private void UseResult(ScanResult? result)
    {
        if (result is null) return;
        ApplyAndSave(result.Config);
        StopScan();
    }

    private void ApplyAndSave(SerialConfig config)
    {
        ApplyConfig(config);
        _profiles.Save(SelectedModel.Key, config);
        Advice = "Configuração salva. Clique em \"Ativar\" e depois em \"Ler peso\" para confirmar.";
        Log($"Configuração aplicada e salva para {SelectedModel.DisplayName}: {config.ShortDescription}.");
    }

    #endregion

    #region Helpers

    private void ShowReading(WeightReading reading)
    {
        // Peso só é exibido quando há valor numérico; status sem peso (IIIII/NNNNN/SSSSS)
        // e "não lido" não mostram número, mas o StatusText informa a situação.
        WeightDisplay = reading.HasWeight
            ? reading.Weight.ToString("0.000", CultureInfo.CurrentCulture) + " " + reading.Unit
            : reading.HasResponse ? "—" : "----";
        StatusText = reading.StatusText;
        LastResponseAscii = reading.RawAscii;
        LastResponseHex = reading.RawHex;
    }

    private static string FormatReading(WeightReading r) =>
        r.HasWeight
            ? $"{r.Weight.ToString("0.000", CultureInfo.InvariantCulture)} {r.Unit} ({r.StatusText})"
            : $"({r.StatusText})";

    private void LogOutcome(SerialReadOutcome outcome)
    {
        // Loga o diagnóstico apenas quando não houve frame, para não inundar o log.
        if (!outcome.Reading.HasResponse)
            Log($"Sem leitura: {outcome.Diagnostics.Summary}");
    }

    private static string EstimateDuration(IReadOnlyCollection<SerialConfig> candidates, int timeoutMsPerAttempt)
    {
        // Estimativa de pior caso: cada candidato podendo esgotar o timeout.
        var worst = TimeSpan.FromMilliseconds((long)candidates.Count * timeoutMsPerAttempt);
        if (worst.TotalMinutes >= 1)
            return $"~{Math.Ceiling(worst.TotalMinutes)} min ({candidates.Count} testes)";
        return $"~{Math.Ceiling(worst.TotalSeconds)} s ({candidates.Count} testes)";
    }

    /// <summary>Encerra sessões abertas (monitor, varredura, porta) ao fechar o app.</summary>
    public void Dispose()
    {
        try { _portWatcher.PortsChanged -= OnPortsChanged; _portWatcher.Dispose(); } catch { }
        try { StopMonitor(); } catch { }
        try { StopScan(); } catch { }
        try { Deactivate(); } catch { }
        _monitorCts?.Dispose();
        _monitorCts = null;
        _scanCts?.Dispose();
        _scanCts = null;
    }

    private void Log(string line)
    {
        try { _logger?.Write(AppLogLevel.Information, "balanca", line); } catch { }
        if (_logSink is null) return;

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) _logSink(line);
        else dispatcher.BeginInvoke(() => _logSink(line));
    }

    #endregion
}
