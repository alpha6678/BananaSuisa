using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Ribanense.Solucoes.App.Winget.Services.Search;

public static class Similarity
{
    private static readonly Regex NonWordRegex = new(@"[^\p{L}\p{N}\s]", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MultipleSpacesRegex = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Normaliza para comparacao: lowercase invariant, remove acentos e
    /// pontuacao, colapsa espacos. "Vs Code" e "vs-code" viram "vs code".
    /// </summary>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        string decomposed = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (char c in decomposed)
        {
            UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        string noAccent = sb.ToString().Normalize(NormalizationForm.FormC);
        string noPunct = NonWordRegex.Replace(noAccent, " ");
        string collapsed = MultipleSpacesRegex.Replace(noPunct, " ").Trim();
        return collapsed.ToLowerInvariant();
    }

    /// <summary>
    /// Jaro similarity (0..1).
    /// </summary>
    public static double Jaro(string a, string b)
    {
        if (a is null) throw new ArgumentNullException(nameof(a));
        if (b is null) throw new ArgumentNullException(nameof(b));
        if (a.Length == 0 && b.Length == 0) return 1.0;
        if (a.Length == 0 || b.Length == 0) return 0.0;

        int matchDistance = Math.Max(a.Length, b.Length) / 2 - 1;
        if (matchDistance < 0) matchDistance = 0;

        bool[] aMatches = new bool[a.Length];
        bool[] bMatches = new bool[b.Length];

        int matches = 0;
        for (int i = 0; i < a.Length; i++)
        {
            int start = Math.Max(0, i - matchDistance);
            int end = Math.Min(i + matchDistance + 1, b.Length);
            for (int j = start; j < end; j++)
            {
                if (bMatches[j]) continue;
                if (a[i] != b[j]) continue;
                aMatches[i] = true;
                bMatches[j] = true;
                matches++;
                break;
            }
        }

        if (matches == 0) return 0.0;

        double transpositions = 0;
        int k = 0;
        for (int i = 0; i < a.Length; i++)
        {
            if (!aMatches[i]) continue;
            while (!bMatches[k]) k++;
            if (a[i] != b[k]) transpositions++;
            k++;
        }
        transpositions /= 2;

        return ((double)matches / a.Length
              + (double)matches / b.Length
              + (matches - transpositions) / matches) / 3.0;
    }

    /// <summary>
    /// Jaro-Winkler com boost padrao (p=0.1) ate 4 caracteres de prefixo comum.
    /// </summary>
    public static double JaroWinkler(string a, string b)
    {
        double jaro = Jaro(a, b);
        int prefix = 0;
        int max = Math.Min(4, Math.Min(a.Length, b.Length));
        for (int i = 0; i < max; i++)
        {
            if (a[i] == b[i]) prefix++;
            else break;
        }
        return jaro + prefix * 0.1 * (1 - jaro);
    }
}
