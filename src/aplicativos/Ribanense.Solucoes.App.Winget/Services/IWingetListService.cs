using Ribanense.Solucoes.App.Winget.Domain;

namespace Ribanense.Solucoes.App.Winget.Services;

public interface IWingetListService
{
    Task<IReadOnlyList<InstalledPackage>> GetInstalledAsync(CancellationToken ct);
}
