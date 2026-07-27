namespace Ribanense.Solucoes.App.Scripts.Scripts.Dns;

public interface IDnsBenchmarkService
{
    /// <summary>
    /// Testa cada servidor informado e retorna os resultados já ranqueados
    /// (melhor primeiro).
    /// </summary>
    Task<IReadOnlyList<DnsServerBenchmarkResult>> RunAsync(
        IReadOnlyList<DnsServerCandidate> servers,
        DnsBenchmarkOptions options,
        IProgress<string>? onLine,
        CancellationToken ct);
}
