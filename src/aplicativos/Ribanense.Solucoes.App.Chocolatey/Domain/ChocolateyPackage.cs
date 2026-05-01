namespace Ribanense.Solucoes.App.Chocolatey.Domain;

/// <summary>Resultado de <c>choco search</c>: pacote disponível em uma fonte Chocolatey.</summary>
public sealed record ChocolateyPackage(
    string Name,
    string Id,
    string Version,
    string Source);

/// <summary>Pacote instalado localmente via Chocolatey.</summary>
public sealed record InstalledChocolateyPackage(
    string Name,
    string Id,
    string InstalledVersion,
    string? AvailableVersion,
    string Source)
{
    public bool HasUpdate =>
        !string.IsNullOrWhiteSpace(AvailableVersion)
        && !string.Equals(AvailableVersion, InstalledVersion, StringComparison.OrdinalIgnoreCase);
}
