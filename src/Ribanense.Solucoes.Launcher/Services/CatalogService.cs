using System.IO;
using System.Text.Json;
using Ribanense.Solucoes.Launcher.Domain;
using Ribanense.Solucoes.PluginSDK.Logging;
using Ribanense.Solucoes.PluginSDK.Vault;
using Json = System.Text.Json.JsonSerializer;

namespace Ribanense.Solucoes.Launcher.Services;

/// <summary>
/// Busca o catalog.json via HTTP (raw.githubusercontent.com), com cache em memoria
/// por 30 min e persistencia no Launcher.dat para funcionar offline.
/// </summary>
public sealed class CatalogService : ICatalogService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);
    private const string CacheJsonKey = "catalog.json.cached";
    private const string CacheTimestampKey = "catalog.json.timestamp";

    private static readonly JsonSerializerOptions ParseOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IGitHubClient _github;
    private readonly IVault _vault;
    private readonly IAppJsonLog _log;
    private readonly string _catalogUrl;
    private readonly object _lock = new();

    private CatalogDocument? _inMemory;
    private DateTime? _inMemoryAt;

    public CatalogService(IGitHubClient github, IVault vault, IAppJsonLog log, string catalogUrl)
    {
        _github = github ?? throw new ArgumentNullException(nameof(github));
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _catalogUrl = string.IsNullOrWhiteSpace(catalogUrl)
            ? throw new ArgumentException("URL obrigatoria.", nameof(catalogUrl))
            : catalogUrl;
    }

    public DateTime? LastRefreshedAtUtc
    {
        get { lock (_lock) return _inMemoryAt; }
    }

    public async Task<CatalogDocument> GetCatalogAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (!forceRefresh && _inMemory is not null && _inMemoryAt is DateTime at
                && DateTime.UtcNow - at < CacheTtl)
            {
                return _inMemory;
            }
        }

        try
        {
            string json = await _github.GetStringAsync(_catalogUrl, ct).ConfigureAwait(false);
            var doc = Parse(json);
            PersistCache(json);

            lock (_lock)
            {
                _inMemory = doc;
                _inMemoryAt = DateTime.UtcNow;
            }

            _log.Write(AppLogLevel.Information, "catalog.fetch", $"Catalogo atualizado com {doc.Apps.Count} apps.");
            return doc;
        }
        catch (Exception ex)
        {
            _log.Write(AppLogLevel.Warning, "catalog.fetch",
                "Falha ao baixar catalogo; tentando usar cache persistido.", ex);

            var fromCache = TryLoadFromVault();
            if (fromCache is not null)
            {
                lock (_lock)
                {
                    _inMemory = fromCache.Value.doc;
                    _inMemoryAt = fromCache.Value.timestamp;
                }
                return fromCache.Value.doc;
            }

            throw;
        }
    }

    private void PersistCache(string json)
    {
        _vault.SetSetting(CacheJsonKey, json);
        _vault.SetSetting(CacheTimestampKey, DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
    }

    private (CatalogDocument doc, DateTime timestamp)? TryLoadFromVault()
    {
        string? cached = _vault.GetSetting(CacheJsonKey);
        if (cached is null) return null;

        try
        {
            var doc = Parse(cached);
            string? ts = _vault.GetSetting(CacheTimestampKey);
            DateTime stamp = DateTime.TryParse(
                ts,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var parsed)
                ? parsed
                : DateTime.MinValue;
            return (doc, stamp);
        }
        catch (Exception parseEx)
        {
            _log.Write(AppLogLevel.Error, "catalog.cache", "Cache do catalogo corrompido; descartando.", parseEx);
            return null;
        }
    }

    private static CatalogDocument Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("catalog.json vazio.");

        var doc = Json.Deserialize<CatalogDocument>(json, ParseOptions)
            ?? throw new InvalidOperationException("catalog.json nao pode ser desserializado.");
        return doc;
    }
}
