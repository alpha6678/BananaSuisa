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

    public string DisplayName => string.IsNullOrWhiteSpace(PublicName) ? Name : PublicName;
}
