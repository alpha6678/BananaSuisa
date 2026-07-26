namespace Ribanense.Solucoes.App.Balanca.Serial;

/// <summary>
/// Porta serial detectada, com nome amigável e identidade estável do dispositivo
/// (PNP ID e VID/PID quando disponíveis) para reconhecer o mesmo adaptador mesmo que
/// o número da porta COM mude após reconexão USB.
/// </summary>
public sealed record SerialPortInfo(
    string Port,
    string? FriendlyName,
    string? PnpDeviceId = null,
    string? Vid = null,
    string? Pid = null)
{
    public string Display =>
        string.IsNullOrWhiteSpace(FriendlyName) ? Port : $"{Port} — {FriendlyName}";

    /// <summary>Identidade estável do dispositivo, independente do número da COM.</summary>
    public string StableId => (Vid, Pid) switch
    {
        (not null, not null) => $"VID_{Vid}&PID_{Pid}",
        _ => PnpDeviceId ?? Port,
    };
}
