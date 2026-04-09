using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BananaSuisa.Core.Text;

public static partial class FuzzyTextMatcher
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        string normalized = text
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);

        StringBuilder builder = new(normalized.Length);
        foreach (char character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC);
    }

    public static bool HasSimilarity(string? search, string? target)
    {
        string searchText = Normalize(search);
        string targetText = Normalize(target);

        if (string.IsNullOrEmpty(searchText) || string.IsNullOrEmpty(targetText))
        {
            return false;
        }

        if (searchText.Length < 2)
        {
            return targetText.Contains(searchText, StringComparison.Ordinal);
        }

        int matchCount = 0;

        for (int i = 0; i < searchText.Length; i++)
        {
            char character = searchText[i];
            int startPosition = Math.Max(0, i - 2);
            int endPosition = Math.Min(targetText.Length - 1, i + 2);

            for (int j = startPosition; j <= endPosition; j++)
            {
                if (targetText[j] == character)
                {
                    matchCount++;
                    break;
                }
            }
        }

        double ratio = (double)matchCount / searchText.Length;
        double threshold = searchText.Length <= 4 ? 0.70 : 0.60;

        return ratio >= threshold;
    }

    public static bool IsFuzzyMatch(string? searchTerm, string? text)
    {
        string normalizedSearch = Normalize(searchTerm);
        string normalizedText = Normalize(text);

        if (string.IsNullOrEmpty(normalizedSearch) || string.IsNullOrEmpty(normalizedText))
        {
            return false;
        }

        if (normalizedText.Contains(normalizedSearch, StringComparison.Ordinal))
        {
            return true;
        }

        if (normalizedSearch.Contains(normalizedText, StringComparison.Ordinal))
        {
            return true;
        }

        string[] searchWords = SplitWords(normalizedSearch);
        string[] textWords = SplitWords(normalizedText);

        foreach (string searchWord in searchWords)
        {
            bool found = false;

            foreach (string textWord in textWords)
            {
                if (textWord.StartsWith(searchWord, StringComparison.Ordinal) ||
                    searchWord.StartsWith(textWord, StringComparison.Ordinal) ||
                    textWord.Contains(searchWord, StringComparison.Ordinal) ||
                    searchWord.Contains(textWord, StringComparison.Ordinal) ||
                    HasSimilarity(searchWord, textWord))
                {
                    found = true;
                    break;
                }
            }

            if (!found && !HasSimilarity(searchWord, normalizedText))
            {
                return false;
            }
        }

        return true;
    }

    private static string[] SplitWords(string text) =>
        WordSeparatorRegex()
            .Split(text)
            .Where(word => word.Length >= 2)
            .ToArray();

    [GeneratedRegex(@"[\s\.\-_]+", RegexOptions.CultureInvariant)]
    private static partial Regex WordSeparatorRegex();
}
