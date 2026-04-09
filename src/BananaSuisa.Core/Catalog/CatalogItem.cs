namespace BananaSuisa.Core.Catalog;

public sealed record CatalogItem(
    string Name,
    string PackageId,
    string Category,
    bool IsEssential,
    string SourceName)
{
    public string SearchText => $"{Name} {PackageId} {Category} {SourceName}";
}
