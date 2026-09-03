namespace Ribanense.Solucoes.App.Farol.Domain;

/// <summary>Pacote UDP anunciado periodicamente na LAN.</summary>
public sealed record FarolHello
{
    public string Kind { get; init; } = "farol-hello";
    public int Protocol { get; init; } = 1;
    public string MachineId { get; init; } = string.Empty;
    public string MachineName { get; init; } = string.Empty;
    public string FriendlyName { get; init; } = string.Empty;
    public string StoreCodeHash { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public int PeerPort { get; init; }
    public DateTimeOffset SentAt { get; init; } = DateTimeOffset.Now;
}

public enum PeerState
{
    Online,
    Ausente,
    Offline,
}

/// <summary>Estado consolidado de um farol vizinho, do ponto de vista desta máquina.</summary>
public sealed record PeerBeacon
{
    public string MachineId { get; init; } = string.Empty;
    public string MachineName { get; init; } = string.Empty;
    public string FriendlyName { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public int PeerPort { get; init; }
    public string Version { get; init; } = string.Empty;
    public DateTimeOffset LastSeen { get; init; }
    public HealthSignal? LastHealth { get; init; }

    public string DisplayName =>
        string.IsNullOrWhiteSpace(FriendlyName) ? MachineName : FriendlyName;

    public PeerState StateAt(DateTimeOffset now, TimeSpan absentAfter, TimeSpan offlineAfter)
    {
        TimeSpan age = now - LastSeen;
        if (age >= offlineAfter) return PeerState.Offline;
        if (age >= absentAfter) return PeerState.Ausente;
        return PeerState.Online;
    }
}
