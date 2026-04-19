namespace Ribanense.Solucoes.App.Winget.Services.Sources;

/// <summary>
/// Uma fonte configurada no winget (resultado de <c>winget source list</c>).
/// </summary>
public sealed record WingetSource(
    string Name,
    string Argument,
    string Type,
    string? TrustLevel = null);
