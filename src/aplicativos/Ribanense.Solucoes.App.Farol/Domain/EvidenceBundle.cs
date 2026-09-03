namespace Ribanense.Solucoes.App.Farol.Domain;

/// <summary>
/// Resultado de um coletor. Um coletor que falha nunca derruba a captura:
/// o dossiê registra o motivo e segue com as demais seções.
/// </summary>
public enum CollectorStatus
{
    Ok,
    Denied,
    Failed,
    Skipped,
}

public sealed record CollectorOutcome(
    string CollectorId,
    string DisplayName,
    CollectorStatus Status,
    string? Detail,
    int DurationMs);

public sealed record IdentityInfo(
    string HostName,
    string UserName,
    string OsDescription,
    string Architecture,
    double UptimeHours,
    string TimeZone,
    DateTimeOffset LocalTime);

public sealed record AdapterInfo(
    string Name,
    string Description,
    string Status,
    string MacAddress,
    IReadOnlyList<string> IpV4,
    IReadOnlyList<string> Gateways,
    IReadOnlyList<string> DnsServers);

/// <summary>Categoria de rede do Windows. Rede Pública bloqueia descoberta na LAN.</summary>
public enum NetworkCategory
{
    Unknown,
    Public,
    Private,
    Domain,
}

public sealed record NetworkInfo(
    NetworkCategory Category,
    string? CategorySource,
    IReadOnlyList<AdapterInfo> Adapters,
    IReadOnlyList<string> ActiveDnsServers,
    string? PrimaryGateway,
    long? GatewayPingMs);

public sealed record DiskInfo(
    string Name,
    string? Label,
    string Format,
    long TotalBytes,
    long FreeBytes)
{
    public double FreePercent => TotalBytes <= 0 ? 0 : Math.Round(FreeBytes * 100.0 / TotalBytes, 1);
}

public sealed record ServiceInfo(
    string Name,
    string DisplayName,
    string Status,
    string StartType);

public sealed record PrinterInfo(
    string Name,
    string? DriverName,
    string? PortName,
    bool IsDefault,
    bool IsOffline,
    bool WorkOffline,
    int QueuedJobs,
    string? StatusText);

public sealed record EventEntryInfo(
    string LogName,
    string Level,
    string Source,
    int EventId,
    DateTimeOffset TimeGenerated,
    string Message);

public sealed record RibanenseAppInfo(
    string AppId,
    string? Version,
    bool VaultPresent,
    int RecentErrorCount,
    string? LastErrorMessage,
    DateTimeOffset? LastErrorAt);

public sealed record ProcessInfo(
    string Name,
    int Id,
    long WorkingSetBytes);

/// <summary>
/// Fotografia estruturada de uma máquina em um instante. É o que trafega entre
/// faróis, o que alimenta as regras e o que vai dentro do ZIP exportado.
/// </summary>
public sealed record EvidenceBundle
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.Now;
    public string MachineId { get; init; } = string.Empty;
    public string MachineName { get; init; } = string.Empty;
    public string FriendlyName { get; init; } = string.Empty;
    public string FarolVersion { get; init; } = string.Empty;

    public IReadOnlyList<CollectorOutcome> Collectors { get; init; } = Array.Empty<CollectorOutcome>();

    public IdentityInfo? Identity { get; init; }
    public NetworkInfo? Network { get; init; }
    public IReadOnlyList<DiskInfo> Disks { get; init; } = Array.Empty<DiskInfo>();
    public IReadOnlyList<ServiceInfo> Services { get; init; } = Array.Empty<ServiceInfo>();
    public IReadOnlyList<PrinterInfo> Printers { get; init; } = Array.Empty<PrinterInfo>();
    public IReadOnlyList<EventEntryInfo> Events { get; init; } = Array.Empty<EventEntryInfo>();
    public IReadOnlyList<RibanenseAppInfo> RibanenseApps { get; init; } = Array.Empty<RibanenseAppInfo>();
    public IReadOnlyList<ProcessInfo> TopProcesses { get; init; } = Array.Empty<ProcessInfo>();

    public ServiceInfo? FindService(string name) =>
        Services.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
}
