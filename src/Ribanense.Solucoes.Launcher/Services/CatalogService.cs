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
    private const string PartnersCacheJsonKey = "parceiros.json.cached";

    private static readonly JsonSerializerOptions ParseOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IGitHubClient _github;
    private readonly IVault _vault;
    private readonly IAppJsonLog _log;
    private readonly string _catalogUrl;
    private readonly string? _bundledJson;
    private readonly string? _partnersUrl;
    private readonly string? _bundledPartnersJson;
    private readonly object _lock = new();

    private CatalogDocument? _inMemory;
    private DateTime? _inMemoryAt;

    public CatalogService(
        IGitHubClient github,
        IVault vault,
        IAppJsonLog log,
        string catalogUrl,
        string? bundledJson = null,
        string? partnersUrl = null,
        string? bundledPartnersJson = null)
    {
        _github = github ?? throw new ArgumentNullException(nameof(github));
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _catalogUrl = string.IsNullOrWhiteSpace(catalogUrl)
            ? throw new ArgumentException("URL obrigatoria.", nameof(catalogUrl))
            : catalogUrl;
        _bundledJson = string.IsNullOrWhiteSpace(bundledJson) ? null : bundledJson;
        _partnersUrl = string.IsNullOrWhiteSpace(partnersUrl) ? null : partnersUrl;
        _bundledPartnersJson = string.IsNullOrWhiteSpace(bundledPartnersJson) ? null : bundledPartnersJson;
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
            return await WithPartnersAsync(doc, forceRefresh, ct).ConfigureAwait(false);
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
                return await WithPartnersAsync(fromCache.Value.doc, forceRefresh, ct).ConfigureAwait(false);
            }

            var fromBundle = TryLoadBundled();
            if (fromBundle is not null)
            {
                _log.Write(AppLogLevel.Information, "catalog.fallback",
                    $"Usando catalogo embutido com {fromBundle.Apps.Count} app(s).");
                lock (_lock)
                {
                    _inMemory = fromBundle;
                    _inMemoryAt = DateTime.UtcNow;
                }
                return await WithPartnersAsync(fromBundle, forceRefresh, ct).ConfigureAwait(false);
            }

            throw;
        }
    }

    private CatalogDocument? TryLoadBundled()
    {
        if (_bundledJson is null) return null;
        try
        {
            return Parse(_bundledJson);
        }
        catch (Exception ex)
        {
            _log.Write(AppLogLevel.Warning, "catalog.bundle",
                "Catalogo embutido invalido; ignorando.", ex);
            return null;
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

    /// <summary>
    /// Entradas de <paramref name="partners"/> substituem o mesmo id no catálogo e as
    /// demais são acrescentadas. Parceiro sempre sai com <c>distribution=partner</c>.
    /// </summary>
    public static CatalogDocument Merge(CatalogDocument catalog, PartnerCatalogDocument? partners)
    {
        if (catalog is null) throw new ArgumentNullException(nameof(catalog));
        if (partners is null || partners.Partners.Count == 0) return catalog;

        var merged = new List<CatalogEntry>();
        var replaced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (CatalogEntry app in catalog.Apps)
        {
            CatalogEntry? partner = partners.Partners.FirstOrDefault(p =>
                string.Equals(p.Id, app.Id, StringComparison.OrdinalIgnoreCase));
            if (partner is null)
            {
                merged.Add(app);
                continue;
            }

            merged.Add(AsPartner(partner));
            replaced.Add(partner.Id);
        }

        foreach (CatalogEntry partner in partners.Partners)
        {
            if (string.IsNullOrWhiteSpace(partner.Id) || replaced.Contains(partner.Id)) continue;
            merged.Add(AsPartner(partner));
            replaced.Add(partner.Id);
        }

        return new CatalogDocument { SchemaVersion = catalog.SchemaVersion, Apps = merged };
    }

    private async Task<CatalogDocument> WithPartnersAsync(CatalogDocument catalog, bool forceRefresh, CancellationToken ct)
    {
        if (_partnersUrl is null && _bundledPartnersJson is null) return catalog;

        PartnerCatalogDocument? partners = await LoadPartnersAsync(forceRefresh, ct).ConfigureAwait(false);
        CatalogDocument merged = Merge(catalog, partners);
        lock (_lock)
        {
            _inMemory = merged;
        }
        return merged;
    }

    private async Task<PartnerCatalogDocument?> LoadPartnersAsync(bool forceRefresh, CancellationToken ct)
    {
        if (!forceRefresh)
        {
            PartnerCatalogDocument? cached = TryParsePartners(_vault.GetSetting(PartnersCacheJsonKey));
            if (cached is not null) return cached;
        }

        if (_partnersUrl is not null)
        {
            try
            {
                string json = await _github.GetStringAsync(_partnersUrl, ct).ConfigureAwait(false);
                PartnerCatalogDocument? parsed = TryParsePartners(json);
                if (parsed is not null)
                {
                    _vault.SetSetting(PartnersCacheJsonKey, json);
                    _log.Write(AppLogLevel.Information, "partners.fetch",
                        $"Parceiros atualizados: {parsed.Partners.Count}.");
                    return parsed;
                }
            }
            catch (Exception ex)
            {
                _log.Write(AppLogLevel.Warning, "partners.fetch",
                    "Falha ao baixar parceiros.json; usando cache ou copia embutida.", ex);
                PartnerCatalogDocument? cached = TryParsePartners(_vault.GetSetting(PartnersCacheJsonKey));
                if (cached is not null) return cached;
            }
        }

        return TryParsePartners(_bundledPartnersJson);
    }

    private PartnerCatalogDocument? TryParsePartners(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return Json.Deserialize<PartnerCatalogDocument>(json, ParseOptions);
        }
        catch (Exception ex)
        {
            _log.Write(AppLogLevel.Warning, "partners.parse", "parceiros.json invalido.", ex);
            return null;
        }
    }

    private static CatalogEntry AsPartner(CatalogEntry partner) =>
        new()
        {
            Id = partner.Id,
            Name = partner.Name,
            PublicName = partner.PublicName,
            Description = partner.Description,
            Category = partner.Category,
            Icon = partner.Icon,
            GithubOwner = partner.GithubOwner,
            GithubRepo = partner.GithubRepo,
            GithubTagPrefix = partner.GithubTagPrefix,
            MinimumLauncherVersion = partner.MinimumLauncherVersion,
            Distribution = "partner",
            InstallerAsset = partner.InstallerAsset,
            InstallerShaAsset = partner.InstallerShaAsset,
            PortableAsset = partner.PortableAsset,
            PortableShaAsset = partner.PortableShaAsset,
            EntryExecutable = partner.EntryExecutable,
            InstallDirectoryName = partner.InstallDirectoryName,
            InstallPathCandidates = partner.InstallPathCandidates ?? new List<string>(),
            UninstallRegistryName = partner.UninstallRegistryName,
            LocalPath = partner.LocalPath
        };
}
