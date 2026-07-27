using Ribanense.Solucoes.App.Scripts.Scripts.Dns;
using Xunit;

namespace Ribanense.Solucoes.App.Scripts.Tests;

public class DnsServerBenchmarkResultTests
{
    private static DnsServerCandidate Candidate() => new("Teste", "1.2.3.4", DnsServerOrigin.Publico);

    [Fact]
    public void Computes_average_median_min_max_from_successful_attempts_only()
    {
        var attempts = new[]
        {
            new DnsQueryAttempt(true, 10, null),
            new DnsQueryAttempt(true, 20, null),
            new DnsQueryAttempt(true, 30, null),
            new DnsQueryAttempt(false, 2000, "timeout"),
        };

        var result = new DnsServerBenchmarkResult(Candidate(), attempts);

        Assert.Equal(4, result.TotalCount);
        Assert.Equal(3, result.SuccessCount);
        Assert.Equal(75.0, result.SuccessRatePercent);
        Assert.Equal(20.0, result.AverageMs);
        Assert.Equal(20.0, result.MedianMs);
        Assert.Equal(10.0, result.MinMs);
        Assert.Equal(30.0, result.MaxMs);
    }

    [Fact]
    public void All_stats_are_null_when_every_attempt_fails()
    {
        var attempts = new[]
        {
            new DnsQueryAttempt(false, 2000, "timeout"),
            new DnsQueryAttempt(false, 2000, "timeout"),
        };

        var result = new DnsServerBenchmarkResult(Candidate(), attempts);

        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(0.0, result.SuccessRatePercent);
        Assert.Null(result.AverageMs);
        Assert.Null(result.MedianMs);
        Assert.Null(result.MinMs);
        Assert.Null(result.MaxMs);
    }

    [Fact]
    public void Median_of_even_count_averages_the_two_middle_values()
    {
        var attempts = new[]
        {
            new DnsQueryAttempt(true, 10, null),
            new DnsQueryAttempt(true, 20, null),
            new DnsQueryAttempt(true, 30, null),
            new DnsQueryAttempt(true, 40, null),
        };

        var result = new DnsServerBenchmarkResult(Candidate(), attempts);

        Assert.Equal(25.0, result.MedianMs);
    }
}
