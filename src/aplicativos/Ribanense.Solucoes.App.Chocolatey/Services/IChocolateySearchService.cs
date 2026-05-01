using Ribanense.Solucoes.App.Chocolatey.Domain;

namespace Ribanense.Solucoes.App.Chocolatey.Services;

public interface IChocolateySearchService
{
    Task<IReadOnlyList<ChocolateyPackage>> SearchAsync(string query, CancellationToken ct);
}
