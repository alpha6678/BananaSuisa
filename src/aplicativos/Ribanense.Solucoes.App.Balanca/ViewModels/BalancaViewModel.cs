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
/// ViewModel principal da tela de teste de balança. Mantém o modo manual clássico
/// e acrescenta os modos automáticos "Um a um" e "Todas as portas".
/// </summary>
public sealed class BalancaViewModel : ObservableObject
{
    private readonly RealSerialChannelFactory _realFactory = new();
    private readonly ProfileStore _profiles;
    private readonly IAppJsonLog? _logger;
    private Action<string>? _logSink;

    private BalancaReader? _reader;
    private CancellationTokenSource? _monitorCts;
    private CancellationTokenSource? _scanCts;

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

        StartScanCommand = new AsyncRelayCommand(_ => StartFullScanAsync(), _ => CanStartScan);
        StopScanCommand = new RelayCommand(StopScan, () => IsScanning);
        StartStepCommand = new AsyncRelayCommand(_ => StartStepScanAsync(), _ => CanStartScan);
        NextStepCommand = new AsyncRelayCommand(_ => NextStepAsync(), _ => IsStepping && !IsBusy);
        UseCurrentConfigCommand = new RelayCommand(UseCurrentCandidate, () => CurrentCandidate is not null);
        UseResultCommand = new RelayCommand(p => UseResult(p as ScanResult), p => p is ScanResult);

        RefreshPorts();
        LoadProfileForModel(SelectedModel);
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
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    private SerialPortInfo? _selectedPort;
    public SerialPortInfo? SelectedPort
    {
        get => _selectedPort;
        set { if (SetProperty(ref _selectedPort, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    private int _baudRate = 9600;
    public int BaudRate { get => _baudRate; set => SetProperty(ref _baudRate, value); }

    private int _dataBits = 8;
    public int DataBits { get => _dataBits; set => SetProperty(ref _dataBits, value); }

    private Parity _parity = Parity.None;
    public Parity Parity { get => _parity; set => SetProperty(ref _parity, value); }

    private StopBits _stopBits = StopBits.One;
    public StopBits StopBits { get => _stopBits; set => SetProperty(ref _stopBits, value); }

    private Handshake _handshake = Handshake.None;
    public Handshake Handshake { get => _handshake; set => SetProperty(ref _handshake, value); }

    private int _timeoutMs = 2000;
    public int TimeoutMs { get => _timeoutMs; set => SetProperty(ref _timeoutMs, value); }

    private bool _deepScan;
    public bool DeepScan { get => _deepScan; set => SetProperty(ref _deepScan, value); }

    #endregion

    #region Modo

    private ScanMode _mode = ScanMode.Manual;
    public ScanMode Mode
    {
        get => _mode;
        set
        {
            if (SetProperty(ref _mode, value))
            {
                OnPropertyChanged(nameof(IsManualMode));
                OnPropertyChanged(nameof(IsUmAUmMode));
                OnPropertyChanged(nameof(IsTodasMode));
                OnPropertyChanged(nameof(IsAutoMode));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsManualMode
    {
        get => Mode == ScanMode.Manual;
        set { if (value) Mode = ScanMode.Manual; }
    }

    public bool IsUmAUmMode
    {
        get => Mode == ScanMode.UmAUm;
        set { if (value) Mode = ScanMode.UmAUm; }
    }

    public bool IsTodasMode
    {
        get => Mode == ScanMode.Todas;
        set { if (value) Mode = ScanMode.Todas; }
    }

    public bool IsAutoMode => Mode != ScanMode.Manual;

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

    public ObservableCollection<ScanResult> ScanResults { get; } = new();

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
            Ports.Add(p);

        SelectedPort = Ports.FirstOrDefault(p => string.Equals(p.Port, previous, StringComparison.OrdinalIgnoreCase))
                       ?? Ports.FirstOrDefault();

        if (Ports.Count == 0)
            Log("Nenhuma porta serial encontrada. Conecte a balança (COM/USB-serial) e clique em Atualizar portas.");
    }

    private void LoadProfileForModel(BalancaModel model)
    {
        var saved = _profiles.TryLoad(model.Key);
        var basis = saved ?? (SelectedPort is not null ? model.DefaultConfig(SelectedPort.Port) : model.DefaultConfig("COM1"));
        ApplyConfig(basis);
        if (saved is not null)
            Log($"Perfil salvo carregado para {model.DisplayName}: {saved.ShortDescription}.");
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
    }

    private SerialConfig BuildConfig() =>
        new(SelectedPort?.Port ?? "COM1", BaudRate, DataBits, Parity, StopBits, Handshake, TimeoutMs);

    #endregion

    #region Modo manual

    private async Task ActivateAsync()
    {
        if (SelectedPort is null) { Log("Selecione uma porta serial."); return; }

        try
        {
            IsBusy = true;
            var config = BuildConfig();
            _reader = new BalancaReader(FactoryFor(SelectedModel));
            await Task.Run(() => _reader.Activate(config, SelectedModel.Protocol)).ConfigureAwait(true);
            IsActive = true;
            Log($"Balança ativada: {SelectedModel.DisplayName} em {config.ShortDescription}.");
        }
        catch (Exception ex)
        {
            _reader?.Dispose();
            _reader = null;
            IsActive = false;
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
    }

    private async Task ReadOnceAsync()
    {
        if (_reader is not { IsActive: true }) return;
        try
        {
            IsBusy = true;
            var reading = await _reader.ReadWeightAsync().ConfigureAwait(true);
            ShowReading(reading);
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
                var reading = await _reader.ReadWeightAsync(ct).ConfigureAwait(true);
                ShowReading(reading);
                await Task.Delay(400, ct).ConfigureAwait(true);
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
    }

    #endregion

    #region Varredura automática

    private async Task StartFullScanAsync()
    {
        var ports = Ports.Select(p => p.Port).ToList();
        if (ports.Count == 0) { Log("Sem portas para varrer."); return; }

        var model = SelectedModel;
        var options = new ScanOptions { Deep = DeepScan, TimeoutMsPerAttempt = Math.Max(400, Math.Min(TimeoutMs, 3000)) };
        var engine = new ScanEngine(FactoryFor(model));
        var candidates = engine.BuildCandidates(model, ports, options);

        ScanResults.Clear();
        IsScanning = true;
        _scanCts = new CancellationTokenSource();
        int total = candidates.Count;
        int done = 0;
        Log($"Varredura completa iniciada: {total} combinações em {ports.Count} porta(s).");

        var progress = new Progress<ScanResult>(r =>
        {
            done++;
            ScanProgress = $"Testando {r.Config.ShortDescription}  ({done}/{total})";
            if (r.Reading.HasResponse)
            {
                ScanResults.Add(r);
                Log($"[hit] {r.Config.ShortDescription} → {FormatReading(r.Reading)}");
            }
        });

        try
        {
            var hits = await engine.ScanAllAsync(model, ports, options, progress, _scanCts.Token);
            ScanResults.Clear();
            foreach (var h in hits) ScanResults.Add(h);
            ScanProgress = $"Concluído: {hits.Count} combinação(ões) com resposta de {total} testadas.";
            Log(ScanProgress);
            if (hits.Count > 0) ShowReading(hits[0].Reading);
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
        if (IsStepping)
        {
            _stepCandidates = Array.Empty<SerialConfig>();
            _stepIndex = -1;
            CurrentCandidate = null;
            StepProgress = "";
            OnPropertyChanged(nameof(IsStepping));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private async Task StartStepScanAsync()
    {
        var ports = Ports.Select(p => p.Port).ToList();
        if (ports.Count == 0) { Log("Sem portas para varrer."); return; }

        var options = new ScanOptions { Deep = DeepScan, TimeoutMsPerAttempt = Math.Max(400, Math.Min(TimeoutMs, 3000)) };
        var engine = new ScanEngine(FactoryFor(SelectedModel));
        _stepCandidates = engine.BuildCandidates(SelectedModel, ports, options);
        _stepIndex = -1;
        ScanResults.Clear();
        OnPropertyChanged(nameof(IsStepping));
        Log($"Varredura passo a passo iniciada: {_stepCandidates.Count} combinações.");
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
            var result = await engine.ProbeAsync(SelectedModel, config).ConfigureAwait(true);
            ShowReading(result.Reading);
            if (result.Reading.HasResponse)
            {
                if (!ScanResults.Any(r => r.Config.ShortDescription == result.Config.ShortDescription))
                    ScanResults.Add(result);
                Log($"[{StepProgress}] {config.ShortDescription} → {FormatReading(result.Reading)}");
            }
            else
            {
                string detail = result.Error is null ? "sem resposta" : result.Error;
                Log($"[{StepProgress}] {config.ShortDescription} → {detail}");
            }
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
        Mode = ScanMode.Manual;
    }

    private void UseResult(ScanResult? result)
    {
        if (result is null) return;
        ApplyAndSave(result.Config);
        StopScan();
        Mode = ScanMode.Manual;
    }

    private void ApplyAndSave(SerialConfig config)
    {
        ApplyConfig(config);
        _profiles.Save(SelectedModel.Key, config);
        Log($"Configuração aplicada e salva para {SelectedModel.DisplayName}: {config.ShortDescription}.");
    }

    #endregion

    #region Helpers

    private void ShowReading(WeightReading reading)
    {
        WeightDisplay = reading.HasResponse
            ? reading.Weight.ToString("0.000", CultureInfo.CurrentCulture) + " " + reading.Unit
            : "----";
        StatusText = reading.StatusText;
        LastResponseAscii = reading.RawAscii;
        LastResponseHex = reading.RawHex;
    }

    private static string FormatReading(WeightReading r) =>
        $"{r.Weight.ToString("0.000", CultureInfo.InvariantCulture)} {r.Unit} ({r.StatusText})";

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
