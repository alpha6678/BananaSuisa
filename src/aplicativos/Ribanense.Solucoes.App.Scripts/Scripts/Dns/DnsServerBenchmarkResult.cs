namespace Ribanense.Solucoes.App.Scripts.Scripts.Dns;

public sealed class DnsServerBenchmarkResult
{
    public DnsServerBenchmarkResult(DnsServerCandidate server, IReadOnlyList<DnsQueryAttempt> attempts)
    {
        Server = server ?? throw new ArgumentNullException(nameof(server));
        Attempts = attempts ?? throw new ArgumentNullException(nameof(attempts));

        var successfulLatencies = attempts.Where(a => a.Success).Select(a => a.ElapsedMs).OrderBy(x => x).ToList();

        TotalCount = attempts.Count;
        SuccessCount = successfulLatencies.Count;
        SuccessRatePercent = TotalCount == 0 ? 0 : (double)SuccessCount / TotalCount * 100.0;

        if (successfulLatencies.Count > 0)
        {
            AverageMs = successfulLatencies.Average();
            MinMs = successfulLatencies[0];
            MaxMs = successfulLatencies[^1];
            MedianMs = ComputeMedian(successfulLatencies);
        }
    }

    public DnsServerCandidate Server { get; }
    public IReadOnlyList<DnsQueryAttempt> Attempts { get; }

    public int TotalCount { get; }
    public int SuccessCount { get; }
    public double SuccessRatePercent { get; }

    public double? AverageMs { get; }
    public double? MedianMs { get; }
    public double? MinMs { get; }
    public double? MaxMs { get; }

    private static double ComputeMedian(IReadOnlyList<double> sortedValues)
    {
        int n = sortedValues.Count;
        int mid = n / 2;
        return n % 2 == 0 ? (sortedValues[mid - 1] + sortedValues[mid]) / 2.0 : sortedValues[mid];
    }
}
