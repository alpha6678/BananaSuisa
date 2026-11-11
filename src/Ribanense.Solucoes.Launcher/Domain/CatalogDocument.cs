using System.Text.Json.Serialization;

namespace Ribanense.Solucoes.Launcher.Domain;

public sealed class CatalogDocument
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = 1;

    [JsonPropertyName("apps")]
    public List<CatalogEntry> Apps { get; init; } = new();
}

public sealed class CatalogEntry
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("publicName")]
    public string? PublicName { get; init; }

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; init; } = string.Empty;

    [JsonPropertyName("icon")]
    public string? Icon { get; init; }

    [JsonPropertyName("githubOwner")]
    public string GithubOwner { get; init; } = string.Empty;

    [JsonPropertyName("githubRepo")]
    public string GithubRepo { get; init; } = string.Empty;

    [JsonPropertyName("githubTagPrefix")]
    public string GithubTagPrefix { get; init; } = string.Empty;

    [JsonPropertyName("minimumLauncherVersion")]
    public string MinimumLauncherVersion { get; init; } = "1.0.0";

    /// <summary><c>module</c> (nosso monorepo) ou <c>partner</c> (repositório de outra pessoa).</summary>
    [JsonPropertyName("distribution")]
    public string Distribution { get; init; } = "module";

    [JsonPropertyName("installerAsset")]
    public string? InstallerAsset { get; init; }

    [JsonPropertyName("installerShaAsset")]
    public string? InstallerShaAsset { get; init; }

    [JsonPropertyName("portableAsset")]
    public string? PortableAsset { get; init; }

    [JsonPropertyName("portableShaAsset")]
    public string? PortableShaAsset { get; init; }

    [JsonPropertyName("entryExecutable")]
    public string? EntryExecutable { get; init; }

    [JsonPropertyName("installDirectoryName")]
    public string? InstallDirectoryName { get; init; }

    [JsonPropertyName("installPathCandidates")]
    public List<string> InstallPathCandidates { get; init; } = new();

    [JsonPropertyName("uninstallRegistryName")]
    public string? UninstallRegistryName { get; init; }

    [JsonPropertyName("localPath")]
    public string? LocalPath { get; init; }

    public bool IsPartner =>
        string.Equals(Distribution, "partner", StringComparison.OrdinalIgnoreCase);

    public string DisplayName => string.IsNullOrWhiteSpace(PublicName) ? Name : PublicName;
}

public sealed class PartnerCatalogDocument
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = 1;

    [JsonPropertyName("partners")]
    public List<CatalogEntry> Partners { get; init; } = new();
}
