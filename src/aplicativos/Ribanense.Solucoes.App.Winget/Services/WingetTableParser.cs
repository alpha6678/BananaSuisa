using System.Text.RegularExpressions;

namespace Ribanense.Solucoes.App.Winget.Services;

/// <summary>
/// Parser do formato tabular que o winget gera em <c>search</c>, <c>list</c> e <c>upgrade</c>.
/// A saída típica tem cabeçalho + linha de separadores "---" seguidos das linhas de dados.
/// As larguras das colunas são inferidas a partir da linha de separadores.
/// </summary>
public static class WingetTableParser
{
    public sealed record Row(IReadOnlyList<string> Values);

    public sealed record Table(IReadOnlyList<string> Headers, IReadOnlyList<Row> Rows);

    private static readonly Regex DashRun = new(@"-{2,}", RegexOptions.Compiled);

    /// <summary>
    /// Recebe a saída completa do winget e devolve a tabela. Retorna <c>null</c>
    /// se não for possível identificar cabeçalho e separadores.
    /// </summary>
    public static Table? Parse(string output)
    {
        if (string.IsNullOrWhiteSpace(output)) return null;

        string[] allLines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        int headerIdx = -1;
        int dashIdx = -1;
        for (int i = 0; i < allLines.Length - 1; i++)
        {
            string maybeDash = allLines[i + 1];
            if (IsDashLine(maybeDash) && !string.IsNullOrWhiteSpace(allLines[i]))
            {
                headerIdx = i;
                dashIdx = i + 1;
                break;
            }
        }

        if (headerIdx < 0) return null;

        var columns = GetColumnRanges(allLines[dashIdx]);
        if (columns.Count == 0) return null;

        string headerLine = allLines[headerIdx];
        var headers = columns.Select(c => Slice(headerLine, c).Trim()).ToList();

        var rows = new List<Row>();
        for (int i = dashIdx + 1; i < allLines.Length; i++)
        {
            string line = allLines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (IsDashLine(line)) continue;

            var values = columns.Select(c => Slice(line, c).Trim()).ToList();
            rows.Add(new Row(values));
        }

        return new Table(headers, rows);
    }

    private static bool IsDashLine(string line)
    {
        string trimmed = line.Trim();
        if (trimmed.Length == 0) return false;
        return trimmed.Replace("-", "").Replace(" ", "").Length == 0
            && trimmed.Contains('-');
    }

    private static List<(int Start, int Length)> GetColumnRanges(string dashLine)
    {
        var ranges = new List<(int Start, int Length)>();
        foreach (Match m in DashRun.Matches(dashLine))
        {
            ranges.Add((m.Index, m.Length));
        }

        // Estende cada coluna até o começo da próxima (para capturar valores mais longos que o cabeçalho).
        for (int i = 0; i < ranges.Count - 1; i++)
        {
            int gapEnd = ranges[i + 1].Start;
            ranges[i] = (ranges[i].Start, gapEnd - ranges[i].Start);
        }
        // A última coluna estende até o fim da linha (ajustado no Slice).
        if (ranges.Count > 0)
        {
            var last = ranges[^1];
            ranges[^1] = (last.Start, int.MaxValue - last.Start);
        }

        return ranges;
    }

    private static string Slice(string line, (int Start, int Length) range)
    {
        if (line.Length <= range.Start) return string.Empty;
        int available = Math.Min(range.Length, line.Length - range.Start);
        return line.Substring(range.Start, available);
    }
}
