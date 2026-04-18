using Ribanense.Solucoes.PluginSDK.Manifest;

namespace Ribanense.Solucoes.Launcher.Domain;

public sealed class InstalledApp
{
    public required AppManifest Manifest { get; init; }
    public required string InstallPath { get; init; }
    public required string ExecutablePath { get; init; }

    public string Id => Manifest.Id;
    public string Version => Manifest.Version;
    public string DisplayName => string.IsNullOrWhiteSpace(Manifest.PublicName) ? Manifest.Name : Manifest.PublicName;
}
