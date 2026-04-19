namespace Ribanense.Solucoes.App.Winget.Services.Diagnostics;

public interface IAppInstallerDiagnostics
{
    Task<AppInstallerStatus> InspectAsync(CancellationToken ct);
}
