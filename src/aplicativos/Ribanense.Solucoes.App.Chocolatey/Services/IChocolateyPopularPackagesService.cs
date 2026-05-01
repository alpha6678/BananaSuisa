using Ribanense.Solucoes.App.Chocolatey.Domain;

namespace Ribanense.Solucoes.App.Chocolatey.Services;

/// <summary>Obtém pacotes mais baixados no repositório da comunidade via feed OData (não via CLI).</summary>
public interface IChocolateyPopularPackagesService
{
    /// <param name="take">Quantidade de pacotes distintos (após deduplicar versões).</param>
    /// <param name="ct">Cancelamento.</param>
    Task<IReadOnlyList<ChocolateyGalleryEntry>> GetMostDownloadedDistinctAsync(int take, CancellationToken ct);
}
