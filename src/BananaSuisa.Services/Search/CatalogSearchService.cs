using BananaSuisa.Core.Catalog;
using BananaSuisa.Core.Text;
using BananaSuisa.Services.Abstractions;

namespace BananaSuisa.Services.Search;

public sealed class CatalogSearchService : ICatalogSearchService
{
    public IReadOnlyList<CatalogItem> Search(CatalogLoadResult catalogLoadResult, string query, int limit = 10)
    {
        ArgumentNullException.ThrowIfNull(catalogLoadResult);

        if (limit <= 0 || string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        string normalizedQuery = FuzzyTextMatcher.Normalize(query);

        return catalogLoadResult.AllItems
            .Where(item => FuzzyTextMatcher.IsFuzzyMatch(normalizedQuery, item.SearchText))
            .OrderBy(item => GetRank(normalizedQuery, item))
            .ThenByDescending(item => item.IsEssential)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();
    }

    public CatalogSearchPreview BuildPreview(CatalogLoadResult catalogLoadResult, int previewLimit = 5)
    {
        ArgumentNullException.ThrowIfNull(catalogLoadResult);

        string previewQuery = ResolvePreviewQuery(catalogLoadResult);
        IReadOnlyList<CatalogItem> previewItems = string.IsNullOrWhiteSpace(previewQuery)
            ? []
            : Search(catalogLoadResult, previewQuery, previewLimit);

        return new CatalogSearchPreview(
            UniqueItemCount: catalogLoadResult.UniqueItemCount,
            CategoryCount: catalogLoadResult.CategoryCount,
            EssentialItemCount: catalogLoadResult.EssentialItemCount,
            PreviewQuery: previewQuery,
            PreviewItems: previewItems);
    }

    private static int GetRank(string normalizedQuery, CatalogItem item)
    {
        string normalizedName = FuzzyTextMatcher.Normalize(item.Name);
        string normalizedId = FuzzyTextMatcher.Normalize(item.PackageId);
        string normalizedCategory = FuzzyTextMatcher.Normalize(item.Category);

        if (normalizedName.Equals(normalizedQuery, StringComparison.Ordinal) ||
            normalizedId.Equals(normalizedQuery, StringComparison.Ordinal))
        {
            return 0;
        }

        if (normalizedName.StartsWith(normalizedQuery, StringComparison.Ordinal) ||
            normalizedId.StartsWith(normalizedQuery, StringComparison.Ordinal))
        {
            return 1;
        }

        if (normalizedName.Contains(normalizedQuery, StringComparison.Ordinal) ||
            normalizedId.Contains(normalizedQuery, StringComparison.Ordinal))
        {
            return 2;
        }

        if (normalizedCategory.Contains(normalizedQuery, StringComparison.Ordinal))
        {
            return 3;
        }

        return 4;
    }

    private static string ResolvePreviewQuery(CatalogLoadResult catalogLoadResult)
    {
        CatalogItem? preferredItem = catalogLoadResult.AllItems
            .OrderByDescending(item => item.IsEssential)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (preferredItem is null)
        {
            return string.Empty;
        }

        string[] words = preferredItem.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length > 0 ? words[0] : preferredItem.Name;
    }
}
