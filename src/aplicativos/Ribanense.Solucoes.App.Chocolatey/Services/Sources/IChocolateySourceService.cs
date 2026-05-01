using Ribanense.Solucoes.App.Chocolatey.Domain;

namespace Ribanense.Solucoes.App.Chocolatey.Services.Sources;

public interface IChocolateySourceService
{
    Task<IReadOnlyList<ChocolateySource>> ListAsync(CancellationToken ct);

    Task<ChocolateyRunResult> RemoveAsync(string name, Action<string>? onLine, CancellationToken ct);
}
