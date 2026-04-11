using System.Text;
using System.Text.RegularExpressions;

namespace BananaSuisa.Infrastructure.WinGet;

/// <summary>
/// Reduz saida do winget (spinners ANSI, barras de progresso, linhas de preenchimento) para o log da UI.
/// </summary>
internal static class WingetInstallOutputSanitizer
{
    private static readonly Regex AnsiEscape = new(@"\x1b\[[0-9;?]*[ -/]*[@-~]", RegexOptions.Compiled);

    public static string SanitizeForLog(string? raw, int maxChars = 8000)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return string.Empty;
        }

        string s = AnsiEscape.Replace(raw, string.Empty);
        s = s.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = s.Split('\n');
        var kept = new List<string>();
        foreach (string line in lines)
        {
            string t = line.TrimEnd();
            if (string.IsNullOrWhiteSpace(t))
            {
                if (kept.Count > 0 && kept[^1].Length > 0)
                {
                    kept.Add(string.Empty);
                }

                continue;
            }

            if (ShouldDropLine(t))
            {
                continue;
            }

            kept.Add(t.Trim());
        }

        while (kept.Count > 0 && string.IsNullOrEmpty(kept[^1]))
        {
            kept.RemoveAt(kept.Count - 1);
        }

        var sb = new StringBuilder();
        var blank = false;
        foreach (string line in kept)
        {
            if (string.IsNullOrEmpty(line))
            {
                if (!blank)
                {
                    sb.AppendLine();
                    blank = true;
                }

                continue;
            }

            blank = false;
            sb.AppendLine(line);
        }

        string result = sb.ToString().Trim();
        if (string.IsNullOrEmpty(result))
        {
            return string.Empty;
        }

        if (result.Length > maxChars)
        {
            return result.Substring(0, maxChars) + "\n... (saida truncada)";
        }

        return result;
    }

    private static bool ShouldDropLine(string line)
    {
        string t = line.Trim();
        if (t.Length == 0)
        {
            return true;
        }

        if (t.Length <= 3 && Regex.IsMatch(t, @"^[-\\|/\s]+$"))
        {
            return true;
        }

        int block = 0;
        int printable = 0;
        foreach (char c in t)
        {
            if (c is '█' or '▒' or '▓' or '░')
            {
                block++;
            }

            if (!char.IsWhiteSpace(c))
            {
                printable++;
            }
        }

        if (printable > 0 && (double)block / printable >= 0.35)
        {
            return true;
        }

        if (t.Length > 80 && t.All(c => char.IsWhiteSpace(c) || c is '█' or '▒' or '▓' or '░'))
        {
            return true;
        }

        if (IsPaddingOnly(t))
        {
            return true;
        }

        return false;
    }

    private static bool IsPaddingOnly(string t)
    {
        int space = 0;
        foreach (char c in t)
        {
            if (c == ' ' || c == '\u00A0')
            {
                space++;
            }
        }

        return space > 0 && space >= t.Length * 0.92;
    }
}
