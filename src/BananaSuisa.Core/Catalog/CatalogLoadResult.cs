namespace BananaSuisa.Core.Catalog;

public sealed record CatalogLoadResult(IReadOnlyList<CatalogSourceResult> Sources)
{
    public bool Succeeded => Sources.All(source => source.Succeeded);

    public IReadOnlyList<CatalogItem> AllItems => Sources
        .SelectMany(source => source.Items)
        .GroupBy(
            item => string.IsNullOrWhiteSpace(item.PackageId) ? item.Name : item.PackageId,
            StringComparer.OrdinalIgnoreCase)
        .Select(group => group.First())
        .ToArray();

    public int UniqueItemCount => AllItems.Count;

    public int CategoryCount => AllItems
        .Select(item => item.Category)
        .Where(category => !string.IsNullOrWhiteSpace(category))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    public int EssentialItemCount => AllItems.Count(item => item.IsEssential);

    public string Summary =>
        Sources.Count == 0
            ? "Nenhum catalogo carregado."
            : $"{string.Join(" | ", Sources.Select(source => $"{source.Name}: {source.ItemCount} item(ns)"))} | Unicos: {UniqueItemCount}";
}
