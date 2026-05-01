using System.Text.RegularExpressions;

namespace Ribanense.Solucoes.App.Winget.Services;

/// <summary>
/// Parser do formato tabular que o winget gera em <c>search</c>, <c>list</c>,
/// <c>upgrade</c> e <c>source list</c>.
///
/// Formato esperado: uma linha de cabecalho com colunas separadas por 2 ou mais
/// espacos, seguida de uma linha de dashes (separada por espacos nas versoes
/// antigas, contigua nas versoes 1.11+), seguida das linhas de dados.
///
/// O parser deduz as colunas a partir das posicoes das palavras do cabecalho
/// (cada palavra ou grupo de palavras separado por 2+ espacos vira uma coluna).
/// Isto funciona em ambos os formatos de dash line, inclusive quando colunas
/// extras como "Correspondencia" aparecem na versao atual do winget.
/// </summary>
public static class WingetTableParser
{
    public sealed record Row(IReadOnlyList<string> Values);

    public sealed record Table(IReadOnlyList<string> Headers, IReadOnlyList<Row> Rows);

    // Match: uma ou mais palavras separadas por UM espaco, terminando antes de
    // 2+ espacos ou do fim da linha. Captura "Trust Level" como uma entrada
    // unica, mas separa "Nome  Id" em duas.
    private static readonly Regex HeaderColumnRegex = new(
        @"\S+(?:\s\S+)*?(?=\s{2,}|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

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

        string headerLine = allLines[headerIdx];
        var columns = GetColumnRangesFromHeader(headerLine);
        if (columns.Count == 0) return null;

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

    /// <summary>
    /// Deriva as ranges das colunas a partir das posicoes das palavras no
    /// cabecalho. Cada palavra (ou grupo separado por apenas 1 espaco) vira
    /// uma coluna. A fronteira entre colunas e a posicao onde surge 2+ espacos
    /// consecutivos.
    /// </summary>
    internal static List<(int Start, int Length)> GetColumnRangesFromHeader(string headerLine)
    {
        var ranges = new List<(int Start, int Length)>();
        if (string.IsNullOrWhiteSpace(headerLine)) return ranges;

        foreach (Match m in HeaderColumnRegex.Matches(headerLine))
        {
            ranges.Add((m.Index, m.Length));
        }

        // Cada coluna estende-se ate o inicio da proxima (para capturar valores
        // mais longos que o cabecalho).
        for (int i = 0; i < ranges.Count - 1; i++)
        {
            int gapEnd = ranges[i + 1].Start;
            ranges[i] = (ranges[i].Start, gapEnd - ranges[i].Start);
        }
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
