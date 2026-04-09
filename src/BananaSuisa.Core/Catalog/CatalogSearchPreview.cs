namespace BananaSuisa.Core.Catalog;

public sealed record CatalogSearchPreview(
    int UniqueItemCount,
    int CategoryCount,
    int EssentialItemCount,
    string PreviewQuery,
    IReadOnlyList<CatalogItem> PreviewItems)
{
    public string Summary
    {
        get
        {
            string querySummary = string.IsNullOrWhiteSpace(PreviewQuery)
                ? "Consulta piloto indisponivel"
                : $"Consulta piloto: {PreviewQuery}";

            return $"Itens unicos: {UniqueItemCount} | Categorias: {CategoryCount} | Essenciais: {EssentialItemCount} | {querySummary} | Matches: {PreviewItems.Count}";
        }
    }
}
