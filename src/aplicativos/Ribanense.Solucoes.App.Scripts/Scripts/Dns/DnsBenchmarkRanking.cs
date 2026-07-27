namespace Ribanense.Solucoes.App.Scripts.Scripts.Dns;

/// <summary>
/// Ordena resultados do benchmark: servidores com sucesso vêm primeiro,
/// ordenados por maior taxa de sucesso e, em seguida, menor latência média.
/// Servidores sem nenhuma resposta bem-sucedida ficam por último.
/// </summary>
public static class DnsBenchmarkRanking
{
    public static IReadOnlyList<DnsServerBenchmarkResult> Rank(IEnumerable<DnsServerBenchmarkResult> results) =>
        results
            .OrderByDescending(r => r.SuccessCount > 0)
            .ThenByDescending(r => r.SuccessRatePercent)
            .ThenBy(r => r.AverageMs ?? double.MaxValue)
            .ToList();
}
