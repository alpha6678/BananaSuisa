using Ribanense.Solucoes.App.Winget.Domain;

namespace Ribanense.Solucoes.App.Winget.Services;

public interface IWingetSearchService
{
    Task<IReadOnlyList<WingetPackage>> SearchAsync(string query, CancellationToken ct);
}
