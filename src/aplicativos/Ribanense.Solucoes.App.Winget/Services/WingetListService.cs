using Ribanense.Solucoes.App.Winget.Domain;

namespace Ribanense.Solucoes.App.Winget.Services;

public sealed class WingetListService : IWingetListService
{
    private readonly IWingetExecutor _executor;

    public WingetListService(IWingetExecutor executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public async Task<IReadOnlyList<InstalledPackage>> GetInstalledAsync(CancellationToken ct)
    {
        var args = new[]
        {
            "list",
            "--disable-interactivity",
            "--accept-source-agreements"
        };

        var result = await _executor.RunAsync(args, ct: ct).ConfigureAwait(false);
        return ParseListOutput(result.Stdout);
    }

    internal static IReadOnlyList<InstalledPackage> ParseListOutput(string stdout)
    {
        var table = WingetTableParser.Parse(stdout);
        if (table is null) return Array.Empty<InstalledPackage>();

        int nameIdx = ColumnIndex(table.Headers, ["Name", "Nome"]);
        int idIdx = ColumnIndex(table.Headers, ["Id", "ID"]);
        int verIdx = ColumnIndex(table.Headers, ["Version", "Versão", "Versao"]);
        int availIdx = ColumnIndex(table.Headers, ["Available", "Disponível", "Disponivel"]);
        int srcIdx = ColumnIndex(table.Headers, ["Source", "Origem", "Fonte"]);

        if (nameIdx < 0 || idIdx < 0 || verIdx < 0) return Array.Empty<InstalledPackage>();

        var list = new List<InstalledPackage>(table.Rows.Count);
        foreach (var row in table.Rows)
        {
            if (row.Values.Count <= Math.Max(Math.Max(nameIdx, idIdx), verIdx)) continue;
            if (string.IsNullOrWhiteSpace(row.Values[idIdx])) continue;

            string? available = availIdx >= 0 && availIdx < row.Values.Count
                ? (string.IsNullOrWhiteSpace(row.Values[availIdx]) ? null : row.Values[availIdx])
                : null;

            string source = srcIdx >= 0 && srcIdx < row.Values.Count ? row.Values[srcIdx] : string.Empty;

            list.Add(new InstalledPackage(
                Name: row.Values[nameIdx],
                Id: row.Values[idIdx],
                InstalledVersion: row.Values[verIdx],
                AvailableVersion: available,
                Source: source));
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
