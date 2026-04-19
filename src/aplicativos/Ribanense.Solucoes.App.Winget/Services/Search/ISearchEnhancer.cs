using Ribanense.Solucoes.App.Winget.Domain;

namespace Ribanense.Solucoes.App.Winget.Services.Search;

public interface ISearchEnhancer
{
    Task<IReadOnlyList<WingetPackage>> SearchAsync(string query, CancellationToken ct);
}
