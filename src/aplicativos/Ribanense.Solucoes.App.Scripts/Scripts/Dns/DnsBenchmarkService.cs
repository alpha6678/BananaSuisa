namespace Ribanense.Solucoes.App.Scripts.Scripts.Dns;

public sealed class DnsBenchmarkService : IDnsBenchmarkService
{
    private readonly IDnsQueryProbe _probe;

    public DnsBenchmarkService(IDnsQueryProbe probe)
    {
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
    }

    public async Task<IReadOnlyList<DnsServerBenchmarkResult>> RunAsync(
        IReadOnlyList<DnsServerCandidate> servers,
        DnsBenchmarkOptions options,
        IProgress<string>? onLine,
        CancellationToken ct)
    {
        if (servers is null || servers.Count == 0)
            throw new ArgumentException("Selecione ao menos um servidor DNS.", nameof(servers));
        options ??= DnsBenchmarkOptions.Default;

        var results = new List<DnsServerBenchmarkResult>();

        foreach (var server in servers)
        {
            ct.ThrowIfCancellationRequested();
            onLine?.Report($"› Testando {server.Label} ({server.IpAddress})...");

            var attempts = new List<DnsQueryAttempt>();
            for (int round = 1; round <= options.RoundsPerServer; round++)
            {
                foreach (var domain in options.Domains)
                {
                    ct.ThrowIfCancellationRequested();

                    var attempt = await _probe.QueryAsync(server.IpAddress, domain, options.Timeout, ct).ConfigureAwait(false);
                    attempts.Add(attempt);

                    string status = attempt.Success ? $"{attempt.ElapsedMs:F0} ms" : $"falhou ({attempt.Error})";
                    onLine?.Report($"    {domain} (rodada {round}): {status}");
                }
            }

            var result = new DnsServerBenchmarkResult(server, attempts);
            results.Add(result);

            onLine?.Report(result.SuccessCount > 0
                ? $"  → média {result.AverageMs:F0} ms, mediana {result.MedianMs:F0} ms, sucesso {result.SuccessRatePercent:F0}%."
                : "  → todas as consultas falharam.");
        }

        return DnsBenchmarkRanking.Rank(results);
    }
}
