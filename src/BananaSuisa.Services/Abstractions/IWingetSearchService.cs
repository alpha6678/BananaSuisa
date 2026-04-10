using BananaSuisa.Core.Winget;

namespace BananaSuisa.Services.Abstractions;

/// <summary>
/// Pesquisa pacotes no repositório configurado do winget (equivalente a <c>winget search</c>).
/// </summary>
public interface IWingetSearchService
{
    /// <summary>
    /// Executa busca por texto. Lista completa estilo "loja" não é suportada de forma eficiente pelo CLI;
    /// use termos de pesquisa (nome, id ou categoria).
    /// </summary>
    Task<WingetSearchOutcome> SearchAsync(string query, int maxResults, CancellationToken cancellationToken = default);
}
