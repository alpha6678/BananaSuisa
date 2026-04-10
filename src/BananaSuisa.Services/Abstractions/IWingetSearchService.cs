using BananaSuisa.Core.Winget;

namespace BananaSuisa.Services.Abstractions;

/// <summary>
/// Pesquisa pacotes no repositório configurado do winget (equivalente a <c>winget search</c>).
/// </summary>
public interface IWingetSearchService
{
    /// <summary>
    /// Executa busca por texto em linguagem natural: palavras de preenchimento são ignoradas na query do winget
    /// e os resultados são ordenados por similaridade ao texto original (nome/ID).
    /// </summary>
    Task<WingetSearchOutcome> SearchAsync(string query, int maxResults, CancellationToken cancellationToken = default);
}
