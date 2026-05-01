using Ribanense.Solucoes.App.Winget.Domain;

namespace Ribanense.Solucoes.App.Winget.Services;

public sealed class WingetSearchService : IWingetSearchService
{
    private readonly IWingetExecutor _executor;

    public WingetSearchService(IWingetExecutor executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public async Task<IReadOnlyList<WingetPackage>> SearchAsync(string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<WingetPackage>();

        var args = new List<string>
        {
            "search",
            query,
            "--disable-interactivity",
            "--accept-source-agreements"
        };

        var result = await _executor.RunAsync(args, ct: ct).ConfigureAwait(false);
        // winget sai com 0x8A15002B quando não encontra; saída continua com "no package found".
        if (!result.Success && string.IsNullOrWhiteSpace(result.Stdout))
        {
            return Array.Empty<WingetPackage>();
        }

        return ParseSearchOutput(result.Stdout);
    }

    internal static IReadOnlyList<WingetPackage> ParseSearchOutput(string stdout)
    {
        var table = WingetTableParser.Parse(stdout);
        if (table is null) return Array.Empty<WingetPackage>();

        int nameIdx = ColumnIndex(table.Headers, ["Name", "Nome"]);
        int idIdx = ColumnIndex(table.Headers, ["Id", "ID"]);
        int verIdx = ColumnIndex(table.Headers, ["Version", "Versão", "Versao"]);
        int srcIdx = ColumnIndex(table.Headers, ["Source", "Origem", "Fonte"]);
        // Coluna "Match"/"Correspondencia" nos resultados recentes do winget.
        // Nao precisa ser extraida, mas se existir desloca a coluna Source.
        _ = ColumnIndex(table.Headers, ["Match", "Correspondência", "Correspondencia"]);

        if (nameIdx < 0 || idIdx < 0 || verIdx < 0) return Array.Empty<WingetPackage>();

        var list = new List<WingetPackage>(table.Rows.Count);
        foreach (var row in table.Rows)
        {
            if (row.Values.Count <= Math.Max(Math.Max(nameIdx, idIdx), verIdx)) continue;
            if (string.IsNullOrWhiteSpace(row.Values[idIdx])) continue;

            list.Add(new WingetPackage(
                Name: row.Values[nameIdx],
                Id: row.Values[idIdx],
                Version: row.Values[verIdx],
                Source: srcIdx >= 0 && srcIdx < row.Values.Count ? row.Values[srcIdx] : string.Empty));
        }
        return list;
    }

    private static int ColumnIndex(IReadOnlyList<string> headers, string[] candidates)
    {
        for (int i = 0; i < headers.Count; i++)
        {
            foreach (var c in candidates)
            {
                if (headers[i].Equals(c, StringComparison.OrdinalIgnoreCase)) return i;
            }
        }
        return -1;
    }
}
