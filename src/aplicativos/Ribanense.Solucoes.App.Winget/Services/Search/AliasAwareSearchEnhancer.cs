using Ribanense.Solucoes.App.Winget.Domain;

namespace Ribanense.Solucoes.App.Winget.Services.Search;

/// <summary>
/// Busca tolerante: normaliza a query, tenta match exato/fuzzy contra um
/// catalogo curado de aliases, roda <see cref="IWingetSearchService"/> para
/// cada candidato e para a query original, depois deduplica por Id com os
/// aliases curados no topo.
/// </summary>
public sealed class AliasAwareSearchEnhancer : ISearchEnhancer
{
    public const double FuzzyThreshold = 0.85;
    public const int MaxFuzzyCandidates = 5;
    public const int MaxFinalResults = 30;

    private readonly IWingetSearchService _search;
    private readonly IAppAliasCatalog _catalog;

    public AliasAwareSearchEnhancer(IWingetSearchService search, IAppAliasCatalog catalog)
    {
        _search = search ?? throw new ArgumentNullException(nameof(search));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public async Task<IReadOnlyList<WingetPackage>> SearchAsync(string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<WingetPackage>();

        string normalized = Similarity.Normalize(query);
        var curatedMatches = ResolveCuratedMatches(normalized);

        // Monta conjunto de queries: ids curados + a query bruta como fallback.
        var queries = new List<string>();
        foreach (var alias in curatedMatches)
        {
            queries.Add(alias.Id);
        }
        queries.Add(query);

        // Dispara buscas em paralelo para reduzir latencia total.
        var tasks = queries.Select(q => _search.SearchAsync(q, ct)).ToList();
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        // Dedup por Id, preservando ordem: primeiro as curadas (na ordem em que resolveram),
        // depois o resto.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var output = new List<WingetPackage>(MaxFinalResults);
        var curatedIds = new HashSet<string>(curatedMatches.Select(a => a.Id), StringComparer.OrdinalIgnoreCase);

        // Pass 1: pacotes cujos Ids correspondem ao conjunto curado (boost).
        foreach (var batch in results)
        {
            foreach (var pkg in batch)
            {
                if (!curatedIds.Contains(pkg.Id)) continue;
                if (seen.Add(pkg.Id))
                {
                    output.Add(pkg);
                    if (output.Count >= MaxFinalResults) return output;
                }
            }
        }

        // Pass 2: o restante.
        foreach (var batch in results)
        {
            foreach (var pkg in batch)
            {
                if (seen.Add(pkg.Id))
                {
                    output.Add(pkg);
                    if (output.Count >= MaxFinalResults) return output;
                }
            }
        }

        return output;
    }

    internal IReadOnlyList<AppAlias> ResolveCuratedMatches(string normalizedQuery)
    {
        if (string.IsNullOrEmpty(normalizedQuery)) return Array.Empty<AppAlias>();

        var exact = new List<AppAlias>();
        var fuzzy = new List<(AppAlias Alias, double Score)>();

        foreach (var alias in _catalog.All)
        {
            bool matched = false;
            double bestScore = 0.0;

            foreach (string syn in alias.Synonyms)
            {
                string normSyn = Similarity.Normalize(syn);
                if (normSyn.Length == 0) continue;

                if (normSyn == normalizedQuery)
                {
                    exact.Add(alias);
                    matched = true;
                    break;
                }

                double score = Similarity.JaroWinkler(normSyn, normalizedQuery);
                if (score > bestScore) bestScore = score;
            }

            // Tambem compara com o publicName e id (normalizados).
            if (!matched && alias.PublicName is not null)
            {
                string normPub = Similarity.Normalize(alias.PublicName);
                if (normPub == normalizedQuery)
                {
                    exact.Add(alias);
                    matched = true;
                }
                else
                {
                    double score = Similarity.JaroWinkler(normPub, normalizedQuery);
                    if (score > bestScore) bestScore = score;
                }
            }

            if (!matched && bestScore >= FuzzyThreshold)
            {
                fuzzy.Add((alias, bestScore));
            }
        }

        // Combina: exatos primeiro, depois fuzzy por score descendente, limitando total.
        var result = new List<AppAlias>();
        result.AddRange(exact);
        foreach (var (alias, _) in fuzzy.OrderByDescending(p => p.Score))
        {
            if (result.Contains(alias)) continue;
            result.Add(alias);
            if (result.Count >= MaxFuzzyCandidates + exact.Count) break;
        }
        return result;
    }
}
