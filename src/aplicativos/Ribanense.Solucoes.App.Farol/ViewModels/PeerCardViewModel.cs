using System.Globalization;
using Ribanense.Solucoes.App.Farol.Domain;
using Ribanense.Solucoes.App.Farol.Mesh;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Farol.ViewModels;

public sealed class PeerCardViewModel : ObservableObject
{
    private PeerBeacon _peer;
    private DateTimeOffset _now;

    public PeerCardViewModel(PeerBeacon peer, DateTimeOffset now)
    {
        _peer = peer ?? throw new ArgumentNullException(nameof(peer));
        _now = now;
    }

    public PeerBeacon Peer => _peer;
    public string MachineId => _peer.MachineId;
    public string DisplayName => _peer.DisplayName;
    public string Address => $"{_peer.Address}:{_peer.PeerPort}";
    public string Version => string.IsNullOrWhiteSpace(_peer.Version) ? "—" : _peer.Version;

    public PeerState State => _peer.StateAt(_now, PeerRegistry.AbsentAfter, PeerRegistry.OfflineAfter);

    public string StateLabel => State switch
    {
        PeerState.Online => "Online",
        PeerState.Ausente => "Ausente",
        _ => "Offline",
    };

    public string LastSeenLabel
    {
        get
        {
            TimeSpan age = _now - _peer.LastSeen;
            if (age < TimeSpan.FromMinutes(1)) return "agora há pouco";
            if (age < TimeSpan.FromHours(1))
                return string.Create(CultureInfo.CurrentCulture, $"há {(int)age.TotalMinutes} min");

            return _peer.LastSeen.ToString("dd/MM HH:mm", CultureInfo.CurrentCulture);
        }
    }

    public string HealthHeadline
    {
        get
        {
            if (_peer.LastHealth is not { } health) return "Sem sinal de saúde ainda.";

            // Um par que sumiu mantém o último sinal: saber que ele estava bem
            // logo antes de desaparecer aponta para queda de máquina ou de rede.
            string headline = health.Headline ?? health.Level.ToString();
            return State == PeerState.Online
                ? headline
                : $"Último sinal antes de sumir: {headline}";
        }
    }

    public HealthLevel HealthLevel => _peer.LastHealth?.Level ?? HealthLevel.Desconhecido;

    public void Update(PeerBeacon peer, DateTimeOffset now)
    {
        _peer = peer;
        _now = now;
        OnPropertyChanged(string.Empty);
    }

    public void Refresh(DateTimeOffset now)
    {
        _now = now;
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(LastSeenLabel));
        OnPropertyChanged(nameof(HealthHeadline));
    }
}
