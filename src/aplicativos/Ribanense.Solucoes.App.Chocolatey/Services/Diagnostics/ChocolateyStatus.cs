namespace Ribanense.Solucoes.App.Chocolatey.Services.Diagnostics;

public sealed record ChocolateyStatus(
    bool Found,
    string? Path,
    string? Version,
    string? Error)
{
    public bool Healthy => Found && !string.IsNullOrWhiteSpace(Version);
}
