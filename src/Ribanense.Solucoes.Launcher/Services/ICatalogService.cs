using Ribanense.Solucoes.Launcher.Domain;

namespace Ribanense.Solucoes.Launcher.Services;

public interface ICatalogService
{
    Task<CatalogDocument> GetCatalogAsync(bool forceRefresh = false, CancellationToken ct = default);
    DateTime? LastRefreshedAtUtc { get; }
}
