using System.Globalization;
using System.Text;

namespace BananaSuisa.Core.Winget;

/// <summary>
/// Monta o texto enviado ao <c>winget search</c> a partir de linguagem natural e
/// ordena os resultados por proximidade ao que o utilizador escreveu (nome/ID).
/// </summary>
public static class WingetSearchRelevance
{
    /// <summary>
    /// Palavras descartadas ao montar a query da CLI (ex.: "navegador chrome" → winget recebe "chrome").
    /// </summary>
    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "o", "a", "os", "as", "de", "do", "da", "dos", "das", "dum", "duma",
        "um", "uma", "uns", "umas", "para", "pra", "com", "por", "sem", "sobre", "entre",
        "no", "na", "nos", "nas", "em", "ao", "aos",
        "e", "ou", "que", "se", "ja", "já",
        "the", "and", "or", "of", "to", "in", "on", "for", "with", "from", "by", "at", "an",
        "navegador", "browser", "aplicativo", "aplicacao", "aplicação", "programa", "software",
        "pacote", "package", "app", "editor",
    };

    /// <summary>
    /// Texto passado ao executável winget (o chamador remove aspas duplas internas).
    /// </summary>
    public static string BuildWingetCliQuery(string userInput)
    {
        string t = userInput.Trim();
        if (string.IsNullOrEmpty(t))
        {
            return t;
        }

        string[] tokens = t.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var keywords = new List<string>(tokens.Length);
        foreach (string tok in tokens)
        {
            if (tok.Length < 2)
            {
                continue;
            }

            if (!Stopwords.Contains(tok))
            {
                keywords.Add(tok);
            }
        }

        return keywords.Count == 0 ? t : string.Join(' ', keywords);
    }

    /// <summary>
    /// Ordena por maior relevância ao texto original e limita a <paramref name="maxResults"/>.
    /// </summary>
    public static IReadOnlyList<WingetSearchItem> RankByRelevance(
        IReadOnlyList<WingetSearchItem> items,
        string originalUserQuery,
        int maxResults)
    {
        if (items.Count == 0 || maxResults < 1)
        {
            return items.Count == 0 ? [] : items.Take(maxResults).ToList();
        }

        string q = originalUserQuery.Trim();
        return items
            .Select(item => (item, Score: ScoreAgainstQuery(q, item)))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.item.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => x.item)
            .Take(maxResults)
            .ToList();
    }

    /// <summary>Exposta para testes unitários.</summary>
    public static int ScoreAgainstQuery(string userQuery, WingetSearchItem item)
    {
        if (string.IsNullOrWhiteSpace(userQuery))
        {
            return 0;
        }

        string q = Normalize(userQuery);
        string name = Normalize(item.Name);
        string id = Normalize(item.Id);
        if (string.IsNullOrEmpty(q))
        {
            return 0;
        }

        int score = 0;

        if (name.Contains(q, StringComparison.Ordinal) || id.Contains(q, StringComparison.Ordinal))
        {
            score += 5000;
        }

        if (name.StartsWith(q, StringComparison.Ordinal) || id.StartsWith(q, StringComparison.Ordinal))
        {
            score += 2500;
        }

        string[] qTokens = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (string token in qTokens)
        {
            if (token.Length < 2 || Stopwords.Contains(token))
            {
                continue;
            }

            if (name.Contains(token, StringComparison.Ordinal) || id.Contains(token, StringComparison.Ordinal))
            {
                score += 900;
            }
            else
            {
                foreach (string term in SplitTerms(name))
                {
                    if (term.Length < 2 || Math.Abs(term.Length - token.Length) > 6)
                    {
                        continue;
                    }

                    if (Levenshtein(token, term) <= 2)
                    {
                        score += 400;
                        break;
                    }
                }

                foreach (string term in SplitTerms(id.Replace('.', ' ')))
                {
                    if (term.Length < 2 || Math.Abs(term.Length - token.Length) > 6)
                    {
                        continue;
                    }

                    if (Levenshtein(token, term) <= 2)
                    {
                        score += 350;
                        break;
                    }
                }
            }
        }

        if (q.Length is >= 3 and <= 48 && name.Length > 0)
        {
            string slice = name.Length > 96 ? name[..96] : name;
            int dist = Levenshtein(q, slice);
            int denom = Math.Max(q.Length, Math.Min(slice.Length, 96));
            if (denom > 0 && dist < denom)
            {
                score += (int)(400 * (1.0 - (double)dist / denom));
            }
        }

        return score;
    }

    private static IEnumerable<string> SplitTerms(string s)
    {
        return s.Split([' ', '.', '-', '_', '/'], StringSplitOptions.RemoveEmptyEntries);
    }

    private static int Levenshtein(string a, string b)
    {
        if (a.Length == 0)
        {
            return b.Length;
        }

        if (b.Length == 0)
        {
            return a.Length;
        }

        int n = a.Length;
        int m = b.Length;
        var dp = new int[n + 1, m + 1];
        for (int i = 0; i <= n; i++)
        {
            dp[i, 0] = i;
        }

        for (int j = 0; j <= m; j++)
        {
            dp[0, j] = j;
        }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }

        return dp[n, m];
    }

    private static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return "";
        }

        var sb = new StringBuilder(s.Length);
        foreach (char c in RemoveDiacritics(s).ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c) || c is ' ' or '.' or '-' or '_')
            {
                sb.Append(c);
            }
            else if (char.IsWhiteSpace(c))
            {
                sb.Append(' ');
            }
        }

        string t = sb.ToString();
        while (t.Contains("  ", StringComparison.Ordinal))
        {
            t = t.Replace("  ", " ", StringComparison.Ordinal);
        }

        return t.Trim();
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (char c in normalized)
        {
            UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
