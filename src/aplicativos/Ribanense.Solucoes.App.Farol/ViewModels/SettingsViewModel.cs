using Ribanense.Solucoes.App.Farol.Configuration;
using Ribanense.Solucoes.App.Farol.Services;
using Ribanense.Solucoes.PluginSDK.Logging;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Farol.ViewModels;

/// <summary>Pareamento, identidade amigável, inicialização automática e firewall.</summary>
public sealed class SettingsViewModel : ObservableObject
{
    private readonly FarolStation _station;
    private readonly AutostartRegistrar _autostart;
    private readonly FirewallRuleInstaller _firewall;
    private readonly IAppJsonLog _log;

    private string _storeCode;
    private string _friendlyName;
    private bool _autostartEnabled;
    private string? _statusMessage;

    public SettingsViewModel(
        FarolStation station,
        AutostartRegistrar autostart,
        FirewallRuleInstaller firewall,
        IAppJsonLog log)
    {
        _station = station ?? throw new ArgumentNullException(nameof(station));
        _autostart = autostart ?? throw new ArgumentNullException(nameof(autostart));
        _firewall = firewall ?? throw new ArgumentNullException(nameof(firewall));
        _log = log ?? throw new ArgumentNullException(nameof(log));

        _storeCode = _station.Pairing.StoreCode ?? string.Empty;
        _friendlyName = _station.Pairing.FriendlyName;
        _autostartEnabled = _autostart.IsEnabled;

        PairCommand = new RelayCommand(Pair, () => !string.IsNullOrWhiteSpace(StoreCode));
        UnpairCommand = new RelayCommand(Unpair, () => _station.Pairing.IsPaired);
        InstallFirewallCommand = new AsyncRelayCommand(InstallFirewallAsync);
    }

    public RelayCommand PairCommand { get; }
    public RelayCommand UnpairCommand { get; }
    public AsyncRelayCommand InstallFirewallCommand { get; }

    public string StoreCode
    {
        get => _storeCode;
        set => SetProperty(ref _storeCode, value);
    }

    public string FriendlyName
    {
        get => _friendlyName;
        set
        {
            if (!SetProperty(ref _friendlyName, value)) return;
            _station.Pairing.FriendlyName = value;
        }
    }

    public bool AutostartEnabled
    {
        get => _autostartEnabled;
        set
        {
            if (!SetProperty(ref _autostartEnabled, value)) return;

            if (_autostart.Set(value))
            {
                StatusMessage = value
                    ? "O Farol vai subir junto com o Windows, na bandeja."
                    : "Inicialização automática desligada.";
            }
            else
            {
                StatusMessage = "O Windows recusou a gravação da inicialização automática.";
                SetProperty(ref _autostartEnabled, _autostart.IsEnabled, nameof(AutostartEnabled));
            }
        }
    }

    public bool IsPaired => _station.Pairing.IsPaired;

    public string MeshStatus => _station.MeshRunning
        ? $"Malha ativa: descoberta UDP {FarolAppConfig.DiscoveryPort}, API TCP {FarolAppConfig.PeerPort}."
        : _station.MeshError ?? "Malha parada. Defina o código da loja para ativá-la.";

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    private void Pair()
    {
        _station.Pairing.Pair(StoreCode);
        _station.StartMesh();

        StatusMessage = "Código salvo. Outras máquinas com o mesmo código aparecem no mapa em até um minuto.";
        _log.Write(AppLogLevel.Information, "pairing", "Código da loja definido.");

        OnPropertyChanged(nameof(IsPaired));
        OnPropertyChanged(nameof(MeshStatus));
    }

    private void Unpair()
    {
        _station.Pairing.Unpair();
        _station.Peers.Clear();

        StatusMessage = "Pareamento removido. Este farol parou de trocar dossiês.";
        _log.Write(AppLogLevel.Information, "pairing", "Pareamento removido.");

        OnPropertyChanged(nameof(IsPaired));
        OnPropertyChanged(nameof(MeshStatus));
    }

    private async Task InstallFirewallAsync()
    {
        StatusMessage = "Aguardando confirmação do Windows para criar as regras…";

        ElevatedResult result = await _firewall.InstallAsync(CancellationToken.None).ConfigureAwait(true);

        if (result.Cancelled)
        {
            StatusMessage = "Você cancelou a elevação. As regras de firewall não foram criadas.";
            return;
        }

        StatusMessage = result.Succeeded
            ? "Regras de entrada criadas para Domínio e Privada, restritas à sub-rede local."
            : "O Windows recusou a criação das regras. Detalhes em --logs.";

        _log.Write(
            result.Succeeded ? AppLogLevel.Information : AppLogLevel.Warning,
            "firewall",
            $"Instalação de regras terminou com código {result.ExitCode}.");
    }
}
