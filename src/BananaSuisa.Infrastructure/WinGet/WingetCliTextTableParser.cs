using System.Text.RegularExpressions;
using BananaSuisa.Core.Winget;

namespace BananaSuisa.Infrastructure.WinGet;

/// <summary>
/// Interpreta tabelas de texto do winget (list/search): colunas alinhadas por posicao; split por espacos
/// remove campos vazios e desloca ID/Versao/Origem.
/// </summary>
/// <remarks>Interno ao assembly; exposto a testes via InternalsVisibleTo.</remarks>
internal static class WingetCliTextTableParser
{
    public static List<WingetSearchItem> TryParsePackageTable(string text, int maxRows)
    {
        string[] lines = text.Split(['\r', '\n'], StringSplitOptions.None);
        int sepIdx = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (IsSeparatorLine(lines[i]))
            {
                sepIdx = i;
                break;
            }
        }

        // Precisamos de pelo menos uma linha de cabecalho antes do separador.
        if (sepIdx < 1)
        {
            return [];
        }

        // Com \r\n o Split pode deixar linha vazia entre cabecalho e ----- ; nao usar sepIdx-1 cegamente.
        int headerIdx = -1;
        for (int j = sepIdx - 1; j >= 0; j--)
        {
            if (!string.IsNullOrWhiteSpace(lines[j]))
            {
                headerIdx = j;
                break;
            }
        }

        if (headerIdx < 0)
        {
            return [];
        }

        string headerLine = lines[headerIdx].TrimEnd();
        if (!LooksLikeWingetTableHeader(headerLine))
        {
            return [];
        }

        ColumnLayout? layout = ColumnLayout.TryCreate(headerLine);
        if (layout is null)
        {
            return [];
        }

        var list = new List<WingetSearchItem>();
        for (int i = sepIdx + 1; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("Nenhum pacote encontrado", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("No package found", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("No packages found", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (line.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsSeparatorLine(line))
            {
                continue;
            }

            string name = layout.GetCell(line, layout.NameIndex);
            string id = layout.GetCell(line, layout.IdIndex);
            string version = layout.GetCell(line, layout.VersionIndex);
            string source = layout.GetCell(line, layout.SourceIndex);

            string origin = WingetInstallationOrigin.Resolve(source, id);
            list.Add(new WingetSearchItem(name, id, version, source, origin));

            if (list.Count >= maxRows)
            {
                break;
            }
        }

        return list;
    }

    private static bool LooksLikeWingetTableHeader(string line)
    {
        bool hasName = line.Contains("Nome", StringComparison.OrdinalIgnoreCase) ||
                       line.Contains("Name", StringComparison.OrdinalIgnoreCase);
        bool hasId = Regex.IsMatch(line, @"\bID\b", RegexOptions.CultureInvariant);
        return hasName && hasId;
    }

    private static bool IsSeparatorLine(string line)
    {
        string trimmed = line.Trim();
        if (trimmed.Length < 5)
        {
            return false;
        }

        return trimmed.All(c => c is '-' or '+' or ' ' or '|');
    }

    private sealed class ColumnLayout
    {
        private ColumnLayout(int[] starts, string[] titles, int nameIdx, int idIdx, int verIdx, int srcIdx)
        {
            Starts = starts;
            Titles = titles;
            NameIndex = nameIdx;
            IdIndex = idIdx;
            VersionIndex = verIdx;
            SourceIndex = srcIdx;
        }

        public int[] Starts { get; }

        public string[] Titles { get; }

        public int NameIndex { get; }

        public int IdIndex { get; }

        public int VersionIndex { get; }

        public int SourceIndex { get; }

        public static ColumnLayout? TryCreate(string headerLine)
        {
            string[] titles = Regex.Split(headerLine.TrimEnd(), @"\s{2,}")
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .ToArray();

            if (titles.Length < 4)
            {
                return null;
            }

            var starts = new int[titles.Length];
            int searchPos = 0;
            for (int i = 0; i < titles.Length; i++)
            {
                int idx = headerLine.IndexOf(titles[i], searchPos, StringComparison.Ordinal);
                if (idx < 0)
                {
                    return null;
                }

                starts[i] = idx;
                searchPos = idx + titles[i].Length;
            }

            int nameIdx = FindTitleIndex(titles, "Nome", "Name");
            int idIdx = FindTitleIndexExact(titles, "ID");
            int verIdx = FindTitleIndex(titles, "Vers\u00E3o", "Version");
            int srcIdx = FindTitleIndex(titles, "Origem", "Source");

            if (nameIdx < 0 || idIdx < 0 || verIdx < 0)
            {
                return null;
            }

            if (srcIdx < 0)
            {
                srcIdx = titles.Length - 1;
            }

            return new ColumnLayout(starts, titles, nameIdx, idIdx, verIdx, srcIdx);
        }

        public string GetCell(string line, int columnIndex)
        {
            if (columnIndex < 0 || columnIndex >= Starts.Length)
            {
                return "";
            }

            int start = Starts[columnIndex];
            int end = columnIndex + 1 < Starts.Length ? Starts[columnIndex + 1] : line.Length;
            end = Math.Min(end, line.Length);
            if (start >= line.Length || end <= start)
            {
                return "";
            }

            return line.AsSpan(start, end - start).Trim().ToString();
        }

        private static int FindTitleIndex(string[] titles, params string[] candidates)
        {
            for (int i = 0; i < titles.Length; i++)
            {
                foreach (string c in candidates)
                {
                    if (titles[i].Equals(c, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private static int FindTitleIndexExact(string[] titles, string exact)
        {
            for (int i = 0; i < titles.Length; i++)
            {
                if (titles[i].Equals(exact, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
