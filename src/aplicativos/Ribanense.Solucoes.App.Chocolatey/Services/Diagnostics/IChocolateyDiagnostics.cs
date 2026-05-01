namespace Ribanense.Solucoes.App.Chocolatey.Services.Diagnostics;

public interface IChocolateyDiagnostics
{
    Task<ChocolateyStatus> InspectAsync(CancellationToken ct);
}
