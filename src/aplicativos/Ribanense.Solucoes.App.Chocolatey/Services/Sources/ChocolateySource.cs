namespace Ribanense.Solucoes.App.Chocolatey.Services.Sources;

/// <summary>Uma fonte configurada no Chocolatey.</summary>
public sealed record ChocolateySource(
    string Name,
    string Url,
    bool Disabled,
    string Priority);
