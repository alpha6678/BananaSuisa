namespace Ribanense.Solucoes.App.Chocolatey.Services;

internal static class ChocolateyLimitedOutputParser
{
    public static IReadOnlyList<string[]> ParsePipeRows(string stdout, int minimumColumns)
    {
        if (string.IsNullOrWhiteSpace(stdout)) return Array.Empty<string[]>();

        var rows = new List<string[]>();
        foreach (string rawLine in stdout.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || !line.Contains('|')) continue;
            if (IsNoise(line)) continue;

            string[] parts = line.Split('|').Select(p => p.Trim()).ToArray();
            if (parts.Length < minimumColumns || parts.Any(string.IsNullOrWhiteSpace)) continue;
            rows.Add(parts);
        }

        return rows;
    }

    private static bool IsNoise(string line)
    {
        string lower = line.ToLowerInvariant();
        return lower.StartsWith("chocolatey ", StringComparison.Ordinal)
            || lower.StartsWith("warning:", StringComparison.Ordinal)
            || lower.StartsWith("error:", StringComparison.Ordinal)
            || lower.Contains(" packages installed.", StringComparison.Ordinal)
            || lower.Contains(" packages found.", StringComparison.Ordinal);
    }
}
