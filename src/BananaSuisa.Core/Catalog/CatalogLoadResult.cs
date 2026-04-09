namespace BananaSuisa.Core.Catalog;

public sealed record CatalogLoadResult(IReadOnlyList<CatalogSourceResult> Sources)
{
    public bool Succeeded => Sources.All(source => source.Succeeded);

    public string Summary =>
        Sources.Count == 0
            ? "Nenhum catalogo carregado."
            : string.Join(" | ", Sources.Select(source => $"{source.Name}: {source.ItemCount} item(ns)"));
}
