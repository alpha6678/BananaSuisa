namespace Ribanense.Solucoes.App.Farol.Domain;

public enum HealthLevel
{
    Ok,
    Degradado,
    Critico,
    Desconhecido,
}

/// <summary>
/// Sinal leve devolvido por <c>GET /health</c>. Serve para o mapa da LAN não
/// precisar baixar um dossiê inteiro só para pintar um card.
/// </summary>
public sealed record HealthSignal
{
    public string MachineId { get; init; } = string.Empty;
    public string MachineName { get; init; } = string.Empty;
    public string FriendlyName { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public HealthLevel Level { get; init; } = HealthLevel.Desconhecido;
    public int HighFindings { get; init; }
    public int MediumFindings { get; init; }
    public string? Headline { get; init; }
    public DateTimeOffset? LastCaptureAt { get; init; }
    public Guid? LastBundleId { get; init; }
    public DateTimeOffset SignalAt { get; init; } = DateTimeOffset.Now;

    public static HealthLevel LevelFor(IReadOnlyList<Finding> findings)
    {
        if (findings.Count == 0) return HealthLevel.Ok;
        if (findings.Any(f => f.Severity == FindingSeverity.High)) return HealthLevel.Critico;
        if (findings.Any(f => f.Severity == FindingSeverity.Medium)) return HealthLevel.Degradado;
        return HealthLevel.Ok;
    }
}
