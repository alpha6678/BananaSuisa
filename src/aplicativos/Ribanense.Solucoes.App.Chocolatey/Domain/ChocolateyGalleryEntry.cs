namespace Ribanense.Solucoes.App.Chocolatey.Domain;

/// <summary>Item do feed OData do Chocolatey Community Repository (NuGet v2).</summary>
public sealed record ChocolateyGalleryEntry(string Id, string Version, long DownloadCount);
