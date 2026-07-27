using Ribanense.Solucoes.App.Scripts.Scripts.Dns;
using Xunit;

namespace Ribanense.Solucoes.App.Scripts.Tests;

public class DnsBenchmarkRankingTests
{
    private static DnsServerCandidate Candidate(string label, string ip) =>
        new(label, ip, DnsServerOrigin.Publico);

    private static DnsQueryAttempt Ok(double ms) => new(true, ms, null);
    private static DnsQueryAttempt Fail(string error = "timeout") => new(false, 2000, error);

    [Fact]
    public void Rank_orders_by_success_rate_then_by_lowest_average_latency()
    {
        var slowButReliable = new DnsServerBenchmarkResult(
            Candidate("Lento", "1.2.3.4"),
            new[] { Ok(120), Ok(130), Ok(110) });

        var fastAndReliable = new DnsServerBenchmarkResult(
            Candidate("Rápido", "5.6.7.8"),
            new[] { Ok(10), Ok(12), Ok(11) });

        var unreliable = new DnsServerBenchmarkResult(
            Candidate("Instável", "9.9.9.9"),
            new[] { Ok(5), Fail(), Fail() });

        var ranked = DnsBenchmarkRanking.Rank(new[] { slowButReliable, unreliable, fastAndReliable });

        Assert.Equal("Rápido", ranked[0].Server.Label);
        Assert.Equal("Lento", ranked[1].Server.Label);
        Assert.Equal("Instável", ranked[2].Server.Label);
    }

    [Fact]
    public void Rank_places_servers_with_zero_successes_last()
    {
        var deadServer = new DnsServerBenchmarkResult(Candidate("Fora do ar", "1.1.1.9"), new[] { Fail(), Fail() });
        var workingServer = new DnsServerBenchmarkResult(Candidate("Funciona", "1.1.1.1"), new[] { Ok(50) });

        var ranked = DnsBenchmarkRanking.Rank(new[] { deadServer, workingServer });

        Assert.Equal("Funciona", ranked[0].Server.Label);
        Assert.Equal("Fora do ar", ranked[1].Server.Label);
    }

    [Fact]
    public void Rank_prefers_higher_success_rate_over_slightly_lower_latency()
    {
        var mostlyFailing = new DnsServerBenchmarkResult(
            Candidate("50% sucesso", "2.2.2.2"),
            new[] { Ok(5), Fail() });

        var fullyReliable = new DnsServerBenchmarkResult(
            Candidate("100% sucesso", "3.3.3.3"),
            new[] { Ok(20), Ok(22) });

        var ranked = DnsBenchmarkRanking.Rank(new[] { mostlyFailing, fullyReliable });

        Assert.Equal("100% sucesso", ranked[0].Server.Label);
    }
}
