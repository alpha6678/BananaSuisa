using System;
using System.IO;
using System.Text.Json;

namespace Ribanense.Solucoes.App.Recuperacao.Recovery.Core;

/// <summary>
/// Pasta de execução de uma sessão de recuperação: concentra relatórios, dumps,
/// logs e o manifesto de backup — equivalente do AudioFix-&lt;stamp&gt; do script.
/// </summary>
public sealed class RunContext
{
    public string RunStamp { get; }
    public string Root { get; }

    private RunContext(string runStamp, string root)
    {
        RunStamp = runStamp;
        Root = root;
    }

    /// <summary>Cria uma nova pasta de execução sob <paramref name="appDataRoot"/>\execucoes.</summary>
    public static RunContext CreateNew(string appDataRoot)
    {
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        string dir = Path.Combine(appDataRoot, "execucoes", stamp);
        Directory.CreateDirectory(dir);
        return new RunContext(stamp, dir);
    }

    /// <summary>
    /// Localiza a execução mais recente que contenha um manifesto de backup
    /// (mesma regra do Test-BackupAvailable do script).
    /// </summary>
    public static RunContext? MostRecentWithManifest(string appDataRoot)
    {
        string baseDir = Path.Combine(appDataRoot, "execucoes");
        if (!Directory.Exists(baseDir)) return null;

        var candidates = Directory.EnumerateDirectories(baseDir)
            .OrderByDescending(Path.GetFileName)
            .Where(d => File.Exists(Path.Combine(d, "backup-manifest.json")))
            .ToList();
        return candidates.Count == 0 ? null : new RunContext(Path.GetFileName(candidates[0]), candidates[0]);
    }

    public string FilePath(string fileName) => Path.Combine(Root, fileName);

    public bool TryReadManifest(out BackupManifest? manifest)
    {
        manifest = null;
        string path = FilePath("backup-manifest.json");
        if (!File.Exists(path)) return false;
        try
        {
            manifest = System.Text.Json.JsonSerializer.Deserialize<BackupManifest>(
                File.ReadAllText(path), BackupManifest.JsonOptions);
            return manifest is not null;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>Estado de um item do backup gravado no manifesto.</summary>
public sealed record BackupManifestItem(string Item, string Status, string Detail);

/// <summary>Manifesto do backup (backup-manifest.json), consultado pelo Reparo/Escalada.</summary>
public sealed record BackupManifest(
    string Timestamp,
    string RunDir,
    string Status,
    IReadOnlyList<BackupManifestItem> Items)
{
    public bool Completo => string.Equals(Status, "Completo", StringComparison.OrdinalIgnoreCase);

    public static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
}
