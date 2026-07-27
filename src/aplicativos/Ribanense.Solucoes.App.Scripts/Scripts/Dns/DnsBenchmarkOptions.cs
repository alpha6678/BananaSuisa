namespace Ribanense.Solucoes.App.Scripts.Scripts.Dns;

public sealed record DnsBenchmarkOptions(
    IReadOnlyList<string> Domains,
    int RoundsPerServer,
    TimeSpan Timeout)
{
    public static DnsBenchmarkOptions Default { get; } = new(
        Domains: new[] { "google.com", "cloudflare.com", "microsoft.com", "wikipedia.org" },
        RoundsPerServer: 3,
        Timeout: TimeSpan.FromSeconds(2));
}
