using Ribanense.Solucoes.App.Chocolatey.Domain;

namespace Ribanense.Solucoes.App.Chocolatey.Services;

public sealed class ChocolateySearchService : IChocolateySearchService
{
    private readonly IChocolateyExecutor _executor;

    public ChocolateySearchService(IChocolateyExecutor executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public async Task<IReadOnlyList<ChocolateyPackage>> SearchAsync(string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<ChocolateyPackage>();

        var args = new[]
        {
            "search",
            query,
            "--limit-output"
        };

        var result = await _executor.RunAsync(args, requireAdmin: false, ct: ct).ConfigureAwait(false);
        if (!result.Success && string.IsNullOrWhiteSpace(result.Stdout))
        {
            return Array.Empty<ChocolateyPackage>();
        }

        return ParseSearchOutput(result.Stdout);
    }

    internal static IReadOnlyList<ChocolateyPackage> ParseSearchOutput(string stdout)
    {
        var rows = ChocolateyLimitedOutputParser.ParsePipeRows(stdout, minimumColumns: 2);
        var list = new List<ChocolateyPackage>(rows.Count);

        foreach (string[] row in rows)
        {
            string id = row[0];
            string version = row[1];
            list.Add(new ChocolateyPackage(
                Name: id,
                Id: id,
                Version: version,
                Source: row.Length > 2 ? row[2] : "Chocolatey"));
        }

        return list;
    }
}
