using BananaSuisa.Core.Catalog;

namespace BananaSuisa.Services.Abstractions;

public interface ICatalogSearchService
{
    IReadOnlyList<CatalogItem> Search(CatalogLoadResult catalogLoadResult, string query, int limit = 10);

    CatalogSearchPreview BuildPreview(CatalogLoadResult catalogLoadResult, int previewLimit = 5);
}
