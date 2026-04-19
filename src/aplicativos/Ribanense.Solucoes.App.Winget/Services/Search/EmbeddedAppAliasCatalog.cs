using System.IO;
using System.Text.Json;
using Json = System.Text.Json.JsonSerializer;

namespace Ribanense.Solucoes.App.Winget.Services.Search;

/// <summary>
/// Carrega o aliases.json embutido como EmbeddedResource no assembly do app.
/// </summary>
public sealed class EmbeddedAppAliasCatalog : IAppAliasCatalog
{
    public const string ResourceName = "Ribanense.Solucoes.App.Winget.aliases.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Lazy<IReadOnlyList<AppAlias>> _lazy;

    public EmbeddedAppAliasCatalog()
    {
        _lazy = new Lazy<IReadOnlyList<AppAlias>>(LoadEmbedded);
    }

    public IReadOnlyList<AppAlias> All => _lazy.Value;

    private static IReadOnlyList<AppAlias> LoadEmbedded()
    {
        try
        {
            var assembly = typeof(EmbeddedAppAliasCatalog).Assembly;
            using var stream = assembly.GetManifestResourceStream(ResourceName);
            if (stream is null) return Array.Empty<AppAlias>();

            using var reader = new StreamReader(stream);
            string json = reader.ReadToEnd();
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<AppAlias>();

            var list = Json.Deserialize<List<AppAlias>>(json, Options);
            return list is null ? Array.Empty<AppAlias>() : list;
        }
        catch
        {
            return Array.Empty<AppAlias>();
        }
    }
}
