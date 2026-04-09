namespace BananaSuisa.Core.Search;

public sealed record ConfigurationSearchEntry(
    string Kind,
    string DisplayText,
    string SearchText,
    string Detail);

public sealed record ConfigurationSearchMatch(
    string Kind,
    string DisplayText,
    string Detail);

public sealed record ConfigurationSearchPreview(
    int ProfileCount,
    int UniqueAppCount,
    int IndexedEntryCount,
    string PreviewQuery,
    IReadOnlyList<ConfigurationSearchMatch> PreviewMatches)
{
    public string Summary
    {
        get
        {
            string querySummary = string.IsNullOrWhiteSpace(PreviewQuery)
                ? "Consulta piloto indisponivel"
                : $"Consulta piloto: {PreviewQuery}";

            return $"Perfis indexados: {ProfileCount} | Apps unicos: {UniqueAppCount} | Entradas pesquisaveis: {IndexedEntryCount} | {querySummary} | Matches: {PreviewMatches.Count}";
        }
    }
}
