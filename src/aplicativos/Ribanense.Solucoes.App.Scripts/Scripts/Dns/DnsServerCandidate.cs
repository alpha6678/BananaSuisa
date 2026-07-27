namespace Ribanense.Solucoes.App.Scripts.Scripts.Dns;

public enum DnsServerOrigin
{
    Publico,
    RedeAtual,
    Personalizado
}

public sealed class DnsServerCandidate
{
    public DnsServerCandidate(string label, string ipAddress, DnsServerOrigin origin)
    {
        if (string.IsNullOrWhiteSpace(ipAddress)) throw new ArgumentException("IP obrigatório.", nameof(ipAddress));
        Label = label ?? throw new ArgumentNullException(nameof(label));
        IpAddress = ipAddress;
        Origin = origin;
    }

    public string Label { get; }
    public string IpAddress { get; }
    public DnsServerOrigin Origin { get; }
}
