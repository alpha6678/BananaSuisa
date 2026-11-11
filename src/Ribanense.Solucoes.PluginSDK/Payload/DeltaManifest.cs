using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ribanense.Solucoes.PluginSDK.Payload;

/// <summary>
/// Diferença entre dois payloads. <see cref="Files"/> lista o que foi adicionado ou
/// alterado; <see cref="Deleted"/> lista caminhos que deixaram de existir.
/// </summary>
public sealed class DeltaManifest
{
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    [JsonPropertyName("fromVersion")]
    public string FromVersion { get; init; } = string.Empty;

    [JsonPropertyName("toVersion")]
    public string ToVersion { get; init; } = string.Empty;

    [JsonPropertyName("files")]
    public List<PayloadFileEntry> Files { get; init; } = new();

    [JsonPropertyName("deleted")]
    public List<string> Deleted { get; init; } = new();

    public static string ZipFileName(string fromVersion) => $"delta-from-{fromVersion}.zip";

    public static string ManifestFileName(string fromVersion) => $"delta-from-{fromVersion}.json";

    public static string ShaFileName(string fromVersion) => $"delta-from-{fromVersion}.zip.sha256";

    /// <summary>Extrai a versão de origem de <c>delta-from-x.y.z.zip</c>. Ignora o <c>.sha256</c>.</summary>
    public static string? TryParseFromVersion(string assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName)) return null;
        string name = assetName.Replace('\\', '/');
        int slash = name.LastIndexOf('/');
        if (slash >= 0) name = name[(slash + 1)..];

        const string prefix = "delta-from-";
        if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) return null;
        if (name.EndsWith(".zip.sha256", StringComparison.OrdinalIgnoreCase)) return null;

        string version = name[prefix.Length..^".zip".Length];
        return string.IsNullOrWhiteSpace(version) ? null : version;
    }

    public static DeltaManifest Diff(PayloadManifest from, PayloadManifest to)
    {
        if (from is null) throw new ArgumentNullException(nameof(from));
        if (to is null) throw new ArgumentNullException(nameof(to));

        var fromByPath = from.Files.ToDictionary(f => f.Path, StringComparer.OrdinalIgnoreCase);
        var toByPath = to.Files.ToDictionary(f => f.Path, StringComparer.OrdinalIgnoreCase);

        var changed = new List<PayloadFileEntry>();
        foreach (var file in to.Files)
        {
            if (!fromByPath.TryGetValue(file.Path, out var previous) ||
                !string.Equals(previous.Sha256, file.Sha256, StringComparison.OrdinalIgnoreCase) ||
                previous.Size != file.Size)
            {
                changed.Add(file);
            }
        }

        changed.Sort(static (a, b) => string.Compare(a.Path, b.Path, StringComparison.Ordinal));

        var deleted = from.Files
            .Select(f => f.Path)
            .Where(path => !toByPath.ContainsKey(path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        return new DeltaManifest
        {
            SchemaVersion = CurrentSchemaVersion,
            FromVersion = from.Version,
            ToVersion = to.Version,
            Files = changed,
            Deleted = deleted
        };
    }

    public static DeltaManifest Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON vazio.", nameof(json));

        return JsonSerializer.Deserialize<DeltaManifest>(json, Options)
            ?? throw new InvalidOperationException("delta-manifest inválido.");
    }

    public static DeltaManifest Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Delta manifest não encontrado.", path);
        return Parse(File.ReadAllText(path));
    }

    public string Serialize() => JsonSerializer.Serialize(this, Options);

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, Serialize());
    }
}
