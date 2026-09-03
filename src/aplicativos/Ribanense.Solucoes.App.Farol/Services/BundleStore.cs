using System.Globalization;
using System.IO;
using Ribanense.Solucoes.App.Farol.Domain;

namespace Ribanense.Solucoes.App.Farol.Services;

public sealed record BundleHandle(Guid Id, DateTimeOffset CapturedAt, string Path);

/// <summary>
/// Histórico de dossiês em disco, um arquivo JSON por captura.
/// </summary>
/// <remarks>
/// Arquivo solto em vez de coleção no vault por dois motivos práticos: o dossiê
/// já é o artefato que vai dentro do ZIP exportado, e um JSON legível pode ser
/// aberto no bloco de notas quando o suporte não tem o app à mão.
/// </remarks>
public sealed class BundleStore
{
    private const string FilePattern = "bundle-*.json";
    private const int DefaultRetention = 30;

    private readonly string _directory;
    private readonly int _retention;
    private readonly object _sync = new();

    public BundleStore(string directory, int retention = DefaultRetention)
    {
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Diretório obrigatório.", nameof(directory));

        _directory = directory;
        _retention = retention < 1 ? DefaultRetention : retention;
        Directory.CreateDirectory(_directory);
    }

    public string Root => _directory;

    public BundleHandle Save(EvidenceBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        string name = string.Create(
            CultureInfo.InvariantCulture,
            $"bundle-{bundle.CapturedAt:yyyyMMdd-HHmmss}-{bundle.Id:N}.json");

        string path = Path.Combine(_directory, name);

        lock (_sync)
        {
            File.WriteAllText(path, FarolJson.Serialize(bundle));
            Prune();
        }

        return new BundleHandle(bundle.Id, bundle.CapturedAt, path);
    }

    public IReadOnlyList<BundleHandle> List()
    {
        lock (_sync)
        {
            return EnumerateFiles()
                .Select(ToHandle)
                .Where(h => h is not null)
                .Select(h => h!)
                .OrderByDescending(h => h.CapturedAt)
                .ToArray();
        }
    }

    public EvidenceBundle? GetLatest()
    {
        BundleHandle? latest = List().FirstOrDefault();
        return latest is null ? null : Load(latest.Path);
    }

    public EvidenceBundle? GetById(Guid id)
    {
        BundleHandle? handle = List().FirstOrDefault(h => h.Id == id);
        return handle is null ? null : Load(handle.Path);
    }

    public static EvidenceBundle? Load(string path)
    {
        try
        {
            return FarolJson.Deserialize<EvidenceBundle>(File.ReadAllText(path));
        }
        catch (IOException)
        {
            return null;
        }
    }

    private IEnumerable<string> EnumerateFiles()
    {
        try
        {
            return Directory.EnumerateFiles(_directory, FilePattern).ToArray();
        }
        catch (DirectoryNotFoundException)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Lê só o cabeçalho do dossiê para montar o índice sem desserializar o
    /// arquivo inteiro: o nome do arquivo já carrega id e instante da captura.
    /// </summary>
    private static BundleHandle? ToHandle(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        string[] parts = name.Split('-');
        if (parts.Length < 4) return null;

        if (!DateTimeOffset.TryParseExact(
                parts[1] + "-" + parts[2],
                "yyyyMMdd-HHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out DateTimeOffset capturedAt))
        {
            return null;
        }

        return Guid.TryParseExact(parts[3], "N", out Guid id)
            ? new BundleHandle(id, capturedAt, path)
            : null;
    }

    private void Prune()
    {
        var stale = EnumerateFiles()
            .Select(ToHandle)
            .Where(h => h is not null)
            .Select(h => h!)
            .OrderByDescending(h => h.CapturedAt)
            .Skip(_retention)
            .ToArray();

        foreach (BundleHandle handle in stale)
        {
            try { File.Delete(handle.Path); }
            catch (IOException) { }
        }
    }
}
