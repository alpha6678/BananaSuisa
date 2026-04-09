using BananaSuisa.Core.Configuration;
using BananaSuisa.Core.Search;
using BananaSuisa.Core.Text;
using BananaSuisa.Services.Abstractions;

namespace BananaSuisa.Services.Search;

public sealed class ConfigurationSearchService : IConfigurationSearchService
{
    public IReadOnlyList<ConfigurationSearchEntry> BuildEntries(BananaSuisaConfig configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        List<ConfigurationSearchEntry> entries = [];

        foreach ((string profileName, BananaSuisaProfile profile) in configuration.Profiles.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            string description = profile.Description?.Trim() ?? string.Empty;
            string searchText = string.IsNullOrWhiteSpace(description)
                ? profileName
                : $"{profileName} {description}";
            string detail = string.IsNullOrWhiteSpace(description)
                ? "Perfil sem descricao."
                : description;

            entries.Add(new ConfigurationSearchEntry(
                Kind: "Perfil",
                DisplayText: profileName,
                SearchText: searchText,
                Detail: detail));
        }

        HashSet<string> uniqueApps = new(StringComparer.OrdinalIgnoreCase);
        AddApps(uniqueApps, configuration.Apps ?? []);

        foreach (BananaSuisaProfile profile in configuration.Profiles.Values)
        {
            AddApps(uniqueApps, profile.Apps ?? []);
        }

        foreach (string appId in uniqueApps.OrderBy(appId => appId, StringComparer.OrdinalIgnoreCase))
        {
            entries.Add(new ConfigurationSearchEntry(
                Kind: "App",
                DisplayText: appId,
                SearchText: appId,
                Detail: "ID do app configurado."));
        }

        return entries;
    }

    public IReadOnlyList<ConfigurationSearchMatch> Search(BananaSuisaConfig configuration, string query, int limit = 10)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (limit <= 0 || string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        IReadOnlyList<ConfigurationSearchEntry> entries = BuildEntries(configuration);
        return Search(entries, query, limit);
    }

    public ConfigurationSearchPreview BuildPreview(BananaSuisaConfig configuration, int previewLimit = 5)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        IReadOnlyList<ConfigurationSearchEntry> entries = BuildEntries(configuration);
        string previewQuery = ResolvePreviewQuery(configuration, entries);
        IReadOnlyList<ConfigurationSearchMatch> previewMatches = string.IsNullOrWhiteSpace(previewQuery)
            ? []
            : Search(entries, previewQuery, previewLimit);
        int uniqueAppCount = entries.Count(entry => entry.Kind == "App");

        return new ConfigurationSearchPreview(
            ProfileCount: configuration.Profiles.Count,
            UniqueAppCount: uniqueAppCount,
            IndexedEntryCount: entries.Count,
            PreviewQuery: previewQuery,
            PreviewMatches: previewMatches);
    }

    private static IReadOnlyList<ConfigurationSearchMatch> Search(IReadOnlyList<ConfigurationSearchEntry> entries, string query, int limit)
    {
        string normalizedQuery = FuzzyTextMatcher.Normalize(query);

        return entries
            .Where(entry => FuzzyTextMatcher.IsFuzzyMatch(normalizedQuery, entry.SearchText))
            .OrderBy(entry => GetRank(normalizedQuery, entry))
            .ThenBy(entry => entry.Kind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.DisplayText, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(entry => new ConfigurationSearchMatch(entry.Kind, entry.DisplayText, entry.Detail))
            .ToArray();
    }

    private static int GetRank(string normalizedQuery, ConfigurationSearchEntry entry)
    {
        string normalizedDisplay = FuzzyTextMatcher.Normalize(entry.DisplayText);
        string normalizedSearch = FuzzyTextMatcher.Normalize(entry.SearchText);

        if (normalizedDisplay.Equals(normalizedQuery, StringComparison.Ordinal))
        {
            return 0;
        }

        if (normalizedDisplay.StartsWith(normalizedQuery, StringComparison.Ordinal))
        {
            return 1;
        }

        if (normalizedSearch.Contains(normalizedQuery, StringComparison.Ordinal))
        {
            return 2;
        }

        return 3;
    }

    private static string ResolvePreviewQuery(BananaSuisaConfig configuration, IReadOnlyList<ConfigurationSearchEntry> entries)
    {
        if (!string.IsNullOrWhiteSpace(configuration.DefaultProfile))
        {
            return configuration.DefaultProfile;
        }

        return entries.FirstOrDefault()?.DisplayText ?? string.Empty;
    }

    private static void AddApps(HashSet<string> apps, IReadOnlyList<string> appIds)
    {
        foreach (string appId in appIds)
        {
            if (!string.IsNullOrWhiteSpace(appId))
            {
                apps.Add(appId.Trim());
            }
        }
    }
}
