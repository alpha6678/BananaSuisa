namespace Ribanense.Solucoes.App.Scripts.Scripts.Dns;

/// <summary>
/// Servidores DNS públicos populares, oferecidos por padrão no teste.
/// </summary>
public static class WellKnownDnsServers
{
    public static IReadOnlyList<DnsServerCandidate> Default { get; } = new List<DnsServerCandidate>
    {
        new("Google", "8.8.8.8", DnsServerOrigin.Publico),
        new("Google", "8.8.4.4", DnsServerOrigin.Publico),
        new("Cloudflare", "1.1.1.1", DnsServerOrigin.Publico),
        new("Cloudflare", "1.0.0.1", DnsServerOrigin.Publico),
        new("Quad9", "9.9.9.9", DnsServerOrigin.Publico),
        new("OpenDNS", "208.67.222.222", DnsServerOrigin.Publico),
        new("OpenDNS", "208.67.220.220", DnsServerOrigin.Publico),
    };
}
