using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ribanense.Solucoes.PluginSDK.Manifest;

/// <summary>
/// POCO do arquivo app.json na raiz de cada app instalado.
/// </summary>
public sealed class AppManifest
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("publicName")]
    public string PublicName { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("minimumLauncherVersion")]
    public string MinimumLauncherVersion { get; init; } = string.Empty;

    [JsonPropertyName("entryExecutable")]
    public string EntryExecutable { get; init; } = string.Empty;

    [JsonPropertyName("icon")]
    public string? Icon { get; init; }

    [JsonPropertyName("category")]
    public string? Category { get; init; }

    [JsonPropertyName("requiresElevation")]
    public bool RequiresElevation { get; init; }

    [JsonPropertyName("githubTagPrefix")]
    public string GithubTagPrefix { get; init; } = string.Empty;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static AppManifest Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON vazio.", nameof(json));

        return JsonSerializer.Deserialize<AppManifest>(json, Options)
            ?? throw new InvalidOperationException("Manifesto inválido: desserialização retornou null.");
    }

    public static AppManifest Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("app.json não encontrado.", path);
        return Parse(File.ReadAllText(path));
    }

    public string Serialize() => JsonSerializer.Serialize(this, Options);

    /// <summary>
    /// Retorna uma lista de mensagens de erro. Vazia = manifesto válido.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Id)) errors.Add("id é obrigatório.");
        if (string.IsNullOrWhiteSpace(Name)) errors.Add("name é obrigatório.");
        if (string.IsNullOrWhiteSpace(PublicName)) errors.Add("publicName é obrigatório.");
        if (string.IsNullOrWhiteSpace(Version)) errors.Add("version é obrigatório.");
        if (string.IsNullOrWhiteSpace(MinimumLauncherVersion)) errors.Add("minimumLauncherVersion é obrigatório.");
        if (string.IsNullOrWhiteSpace(EntryExecutable)) errors.Add("entryExecutable é obrigatório.");
        if (string.IsNullOrWhiteSpace(GithubTagPrefix)) errors.Add("githubTagPrefix é obrigatório.");

        if (!string.IsNullOrWhiteSpace(Version) && !System.Version.TryParse(Version, out _) &&
            !SemVerLoose.IsValid(Version))
        {
            errors.Add($"version '{Version}' não é SemVer válido.");
        }

        if (!string.IsNullOrWhiteSpace(MinimumLauncherVersion) &&
            !System.Version.TryParse(MinimumLauncherVersion, out _) &&
            !SemVerLoose.IsValid(MinimumLauncherVersion))
        {
            errors.Add($"minimumLauncherVersion '{MinimumLauncherVersion}' não é SemVer válido.");
        }

        return errors;
    }
}
