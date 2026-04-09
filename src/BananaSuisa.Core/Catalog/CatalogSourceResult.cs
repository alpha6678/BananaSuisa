namespace BananaSuisa.Core.Catalog;

public sealed record CatalogSourceResult(
    string Name,
    bool Succeeded,
    string SourcePath,
    int ItemCount,
    string Detail,
    IReadOnlyList<CatalogItem> Items);
