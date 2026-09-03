using System.Collections.Concurrent;
using Ribanense.Solucoes.App.Farol.Domain;

namespace Ribanense.Solucoes.App.Farol.Mesh;

/// <summary>
/// Faróis conhecidos nesta rede. Um par nunca é removido por silêncio: ele passa
/// a "ausente" e depois "offline" mantendo o último sinal, porque saber que a
/// máquina sumiu com saúde OK é justamente a informação útil.
/// </summary>
public sealed class PeerRegistry
{
    public static readonly TimeSpan AbsentAfter = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan OfflineAfter = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, PeerBeacon> _peers = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _selfMachineId;

    public PeerRegistry(string selfMachineId)
    {
        _selfMachineId = selfMachineId ?? throw new ArgumentNullException(nameof(selfMachineId));
    }

    public event Action<IReadOnlyList<PeerBeacon>>? Changed;

    public IReadOnlyList<PeerBeacon> Snapshot() =>
        _peers.Values.OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToArray();

    public PeerBeacon? Find(string machineId) =>
        _peers.TryGetValue(machineId, out PeerBeacon? peer) ? peer : null;

    /// <summary>Registra o beacon de um par. Ignora o eco do próprio anúncio.</summary>
    public bool Observe(FarolHello hello, string address, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(hello);

        if (string.IsNullOrWhiteSpace(hello.MachineId)) return false;
        if (string.Equals(hello.MachineId, _selfMachineId, StringComparison.OrdinalIgnoreCase)) return false;

        bool isNew = !_peers.ContainsKey(hello.MachineId);

        _peers.AddOrUpdate(
            hello.MachineId,
            _ => new PeerBeacon
            {
                MachineId = hello.MachineId,
                MachineName = hello.MachineName,
                FriendlyName = hello.FriendlyName,
                Address = address,
                PeerPort = hello.PeerPort,
                Version = hello.Version,
                LastSeen = now,
            },
            (_, existing) => existing with
            {
                MachineName = hello.MachineName,
                FriendlyName = hello.FriendlyName,
                Address = address,
                PeerPort = hello.PeerPort,
                Version = hello.Version,
                LastSeen = now,
            });

        Changed?.Invoke(Snapshot());
        return isNew;
    }

    public void AttachHealth(string machineId, HealthSignal health)
    {
        if (!_peers.TryGetValue(machineId, out PeerBeacon? existing)) return;

        _peers[machineId] = existing with { LastHealth = health };
        Changed?.Invoke(Snapshot());
    }

    public void Forget(string machineId)
    {
        if (_peers.TryRemove(machineId, out _)) Changed?.Invoke(Snapshot());
    }

    public void Clear()
    {
        _peers.Clear();
        Changed?.Invoke(Snapshot());
    }
}
