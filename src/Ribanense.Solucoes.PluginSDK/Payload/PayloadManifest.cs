using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ribanense.Solucoes.PluginSDK.Payload;

/// <summary>
/// Inventário de um payload publicado: cada arquivo com tamanho e SHA256.
/// O próprio <c>payload-manifest.json</c> não entra na lista (o hash seria circular).
/// </summary>
public sealed class PayloadManifest
{
    public const string FileName = "payload-manifest.json";
    public const int CurrentSchemaVersion = 1;

    /// <summary>Pastas de dados do usuário que nunca fazem parte do payload.</summary>
    public static readonly string[] PreservedDirectoryNames = ["dados", "aplicativos"];

    public const string PortableMarkerFileName = "portable.flag";
    public const string InstallModeFileName = "install-mode.txt";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("entryExecutable")]
    public string EntryExecutable { get; init; } = string.Empty;

    [JsonPropertyName("files")]
    public List<PayloadFileEntry> Files { get; init; } = new();

    public static PayloadManifest Compute(string root, string version, string entryExecutable)
    {
        if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("Pasta obrigatória.", nameof(root));
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        if (string.IsNullOrWhiteSpace(version)) throw new ArgumentException("Versão obrigatória.", nameof(version));
        if (string.IsNullOrWhiteSpace(entryExecutable)) throw new ArgumentException("Executável obrigatório.", nameof(entryExecutable));

        string fullRoot = Path.GetFullPath(root);
        var files = new List<PayloadFileEntry>();

        foreach (string file in Directory.EnumerateFiles(fullRoot, "*", SearchOption.AllDirectories))
        {
            string relative = ToRelative(fullRoot, file);
            if (!IsPayloadPath(relative)) continue;

            var info = new FileInfo(file);
            files.Add(new PayloadFileEntry
            {
                Path = relative,
                Size = info.Length,
                Sha256 = HashFile(file)
            });
        }

        files.Sort(static (a, b) => string.Compare(a.Path, b.Path, StringComparison.Ordinal));

        return new PayloadManifest
        {
            SchemaVersion = CurrentSchemaVersion,
            Version = version.Trim(),
            EntryExecutable = entryExecutable.Replace('\\', '/'),
            Files = files
        };
    }

    public static PayloadManifest Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON vazio.", nameof(json));

        return JsonSerializer.Deserialize<PayloadManifest>(json, Options)
            ?? throw new InvalidOperationException("payload-manifest.json inválido.");
    }

    public static PayloadManifest Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("payload-manifest.json não encontrado.", path);
        return Parse(File.ReadAllText(path));
    }

    public string Serialize() => JsonSerializer.Serialize(this, Options);

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, Serialize());
    }

    public PayloadFileEntry? Find(string relativePath)
    {
        string normalized = NormalizeRelative(relativePath);
        return Files.FirstOrDefault(f => string.Equals(f.Path, normalized, StringComparison.OrdinalIgnoreCase));
    }

    public static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static string ToRelative(string root, string file)
    {
        string relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(file));
        return relative.Replace('\\', '/');
    }

    /// <summary>
    /// Caminho relativo seguro de payload: sem absoluto, sem <c>..</c> e fora das pastas de dados.
    /// </summary>
    public static bool IsPayloadPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return false;
        string normalized = relativePath.Replace('\\', '/').TrimStart('/');
        if (normalized.Contains(':')) return false;

        string[] parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;
        if (parts.Any(p => p == "." || p == "..")) return false;

        if (string.Equals(normalized, FileName, StringComparison.OrdinalIgnoreCase)) return false;
        if (string.Equals(normalized, PortableMarkerFileName, StringComparison.OrdinalIgnoreCase)) return false;
        if (string.Equals(normalized, InstallModeFileName, StringComparison.OrdinalIgnoreCase)) return false;
        if (IsPreservedPath(normalized)) return false;
        return true;
    }

    public static bool IsPreservedPath(string relativePath)
    {
        string normalized = relativePath.Replace('\\', '/').TrimStart('/');
        foreach (string name in PreservedDirectoryNames)
        {
            if (string.Equals(normalized, name, StringComparison.OrdinalIgnoreCase)) return true;
            if (normalized.StartsWith(name + "/", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    public static string NormalizeRelative(string relativePath) =>
        relativePath.Replace('\\', '/').TrimStart('/');
}

public sealed class PayloadFileEntry
{
    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; init; }

    [JsonPropertyName("sha256")]
    public string Sha256 { get; init; } = string.Empty;
}
