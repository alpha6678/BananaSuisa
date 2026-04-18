namespace Ribanense.Solucoes.App.Winget.Domain;

/// <summary>Resultado de <c>winget search</c>: pacote disponível em algum source.</summary>
public sealed record WingetPackage(
    string Name,
    string Id,
    string Version,
    string Source);

/// <summary>Pacote instalado localmente (<c>winget list</c>).</summary>
public sealed record InstalledPackage(
    string Name,
    string Id,
    string InstalledVersion,
    string? AvailableVersion,
    string Source)
{
    public bool HasUpdate =>
        !string.IsNullOrWhiteSpace(AvailableVersion)
        && !string.Equals(AvailableVersion, InstalledVersion, StringComparison.Ordinal);
}
