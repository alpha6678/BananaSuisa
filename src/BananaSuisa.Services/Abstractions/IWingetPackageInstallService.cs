using BananaSuisa.Core.Winget;

namespace BananaSuisa.Services.Abstractions;

/// <summary>
/// Instala um pacote via <c>winget install</c> (equivalente à linha de comandos).
/// </summary>
public interface IWingetPackageInstallService
{
    Task<WingetInstallOutcome> InstallAsync(string packageId, string? source, CancellationToken cancellationToken = default);
}
