namespace Ribanense.Solucoes.App.Balanca.Serial;

/// <summary>
/// Porta serial detectada, com nome amigável quando disponível (ex.: USB-serial).
/// </summary>
public sealed record SerialPortInfo(string Port, string? FriendlyName)
{
    public string Display =>
        string.IsNullOrWhiteSpace(FriendlyName) ? Port : $"{Port} — {FriendlyName}";
}
