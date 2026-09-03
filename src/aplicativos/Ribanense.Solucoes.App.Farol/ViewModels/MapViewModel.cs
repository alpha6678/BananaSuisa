using System.Collections.ObjectModel;
using System.Windows.Threading;
using Ribanense.Solucoes.App.Farol.Configuration;
using Ribanense.Solucoes.App.Farol.Domain;
using Ribanense.Solucoes.App.Farol.Services;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Farol.ViewModels;

/// <summary>Mapa dos faróis vistos nesta rede.</summary>
public sealed class MapViewModel : ObservableObject
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(15);

    private readonly FarolStation _station;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _timer;
    private string? _statusMessage;

    public MapViewModel(FarolStation station, Dispatcher dispatcher)
    {
        _station = station ?? throw new ArgumentNullException(nameof(station));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);

        _station.Peers.Changed += OnPeersChanged;

        // Estado dos cards depende do relógio, não só de eventos: sem este tick
        // um par que parou de anunciar continuaria pintado como online.
        _timer = new DispatcherTimer(TickInterval, DispatcherPriority.Background, (_, _) => Tick(), _dispatcher);
        _timer.Start();

        Apply(_station.Peers.Snapshot());
    }

    public ObservableCollection<PeerCardViewModel> Peers { get; } = new();

    public AsyncRelayCommand RefreshCommand { get; }

    public bool HasPeers => Peers.Count > 0;

    public string EmptyMessage =>
        !_station.Pairing.IsPaired
            ? "Defina o código da loja em Ajustes para este farol entrar na malha."
            : _station.MeshError is { } error
                ? $"A malha não subiu: {error}"
                : $"Nenhum outro farol respondeu ainda. Confira se as outras máquinas usam o mesmo código e se a porta UDP {FarolAppConfig.DiscoveryPort} está liberada.";

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    private async Task RefreshAsync()
    {
        StatusMessage = "Anunciando na rede…";
        await _station.AnnounceAsync().ConfigureAwait(true);

        DateTimeOffset now = DateTimeOffset.Now;
        foreach (PeerCardViewModel card in Peers.ToArray())
        {
            HealthSignal? health = await _station.Client
                .GetHealthAsync(card.Peer, CancellationToken.None)
                .ConfigureAwait(true);

            if (health is not null) _station.Peers.AttachHealth(card.MachineId, health);
            card.Refresh(now);
        }

        StatusMessage = Peers.Count == 0
            ? "Nenhum farol respondeu."
            : $"{Peers.Count} farol(óis) na rede.";
    }

    private void OnPeersChanged(IReadOnlyList<PeerBeacon> peers)
    {
        if (_dispatcher.CheckAccess()) Apply(peers);
        else _dispatcher.BeginInvoke(() => Apply(peers));
    }

    private void Apply(IReadOnlyList<PeerBeacon> peers)
    {
        DateTimeOffset now = DateTimeOffset.Now;

        foreach (PeerBeacon peer in peers)
        {
            PeerCardViewModel? existing = Peers.FirstOrDefault(c => c.MachineId == peer.MachineId);
            if (existing is null) Peers.Add(new PeerCardViewModel(peer, now));
            else existing.Update(peer, now);
        }

        var known = peers.Select(p => p.MachineId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (PeerCardViewModel stale in Peers.Where(c => !known.Contains(c.MachineId)).ToArray())
        {
            Peers.Remove(stale);
        }

        OnPropertyChanged(nameof(HasPeers));
        OnPropertyChanged(nameof(EmptyMessage));
    }

    private void Tick()
    {
        DateTimeOffset now = DateTimeOffset.Now;
        foreach (PeerCardViewModel card in Peers) card.Refresh(now);
    }

    public void Detach()
    {
        _timer.Stop();
        _station.Peers.Changed -= OnPeersChanged;
    }
}
