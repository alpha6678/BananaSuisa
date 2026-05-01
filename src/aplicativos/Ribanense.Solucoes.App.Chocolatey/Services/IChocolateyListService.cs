using Ribanense.Solucoes.App.Chocolatey.Domain;

namespace Ribanense.Solucoes.App.Chocolatey.Services;

public interface IChocolateyListService
{
    Task<IReadOnlyList<InstalledChocolateyPackage>> GetInstalledAsync(CancellationToken ct);
}
