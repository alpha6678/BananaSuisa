using System.Text.Json;
using System.Text.Json.Serialization;
using Ribanense.Solucoes.Launcher.Domain;
using Ribanense.Solucoes.PluginSDK.Manifest;
using Json = System.Text.Json.JsonSerializer;

namespace Ribanense.Solucoes.Launcher.Services;

/// <summary>
/// Consulta releases do GitHub via API publica (api.github.com) e filtra por prefixo de tag.
/// </summary>
public sealed class ReleaseCheckService : IReleaseCheckService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IGitHubClient _github;

    public ReleaseCheckService(IGitHubClient github)
    {
        _github = github ?? throw new ArgumentNullException(nameof(github));
    }

    public async Task<IReadOnlyList<ReleaseInfo>> GetAllReleasesAsync(
        string owner, string repo, string tagPrefix, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("owner obrigatorio.", nameof(owner));
        if (string.IsNullOrWhiteSpace(repo)) throw new ArgumentException("repo obrigatorio.", nameof(repo));
        if (string.IsNullOrWhiteSpace(tagPrefix)) throw new ArgumentException("tagPrefix obrigatorio.", nameof(tagPrefix));

        string url = $"https://api.github.com/repos/{owner}/{repo}/releases?per_page=100";
        string json = await _github.GetStringAsync(url, ct).ConfigureAwait(false);

        var items = Json.Deserialize<List<GhRelease>>(json, Options) ?? new();
        var result = new List<ReleaseInfo>();

        foreach (var r in items)
        {
            if (r.Draft) continue;
            if (string.IsNullOrWhiteSpace(r.TagName)) continue;
            if (!r.TagName.StartsWith(tagPrefix, StringComparison.Ordinal)) continue;

            string version = r.TagName.Substring(tagPrefix.Length);
            if (!SemVerLoose.IsValid(version)) continue;

            var assets = (r.Assets ?? new List<GhAsset>())
                .Select(a => new ReleaseAsset
                {
                    Name = a.Name ?? string.Empty,
                    DownloadUrl = a.BrowserDownloadUrl ?? string.Empty,
                    Size = a.Size
                })
                .ToList();

            result.Add(new ReleaseInfo
            {
                Tag = r.TagName,
                Version = version,
                Name = string.IsNullOrWhiteSpace(r.Name) ? r.TagName : r.Name,
                IsPrerelease = r.Prerelease,
                PublishedAtUtc = r.PublishedAt ?? DateTime.MinValue,
                Assets = assets
            });
        }

        return result;
    }

    public async Task<ReleaseInfo?> GetLatestReleaseAsync(
        string owner, string repo, string tagPrefix, bool includePrerelease, CancellationToken ct)
    {
        var all = await GetAllReleasesAsync(owner, repo, tagPrefix, ct).ConfigureAwait(false);
        var filtered = includePrerelease ? all : all.Where(r => !r.IsPrerelease);
        return filtered
            .OrderByDescending(r => r.Version, SemVerComparer.Instance)
            .FirstOrDefault();
    }

    public UpdateStatus CompareVersions(string? installedVersion, string? latestVersion)
    {
        if (string.IsNullOrWhiteSpace(installedVersion))
            return UpdateStatus.NotInstalled;

        if (string.IsNullOrWhiteSpace(latestVersion))
            return UpdateStatus.ReleaseNotFound;

        if (!SemVerLoose.IsValid(installedVersion) || !SemVerLoose.IsValid(latestVersion))
            return UpdateStatus.CorruptedInstallation;

        int cmp = SemVerLoose.Compare(installedVersion, latestVersion);
        return cmp < 0 ? UpdateStatus.UpdateAvailable : UpdateStatus.UpToDate;
    }

    private sealed class GhRelease
    {
        [JsonPropertyName("tag_name")] public string TagName { get; init; } = string.Empty;
        [JsonPropertyName("name")] public string? Name { get; init; }
        [JsonPropertyName("draft")] public bool Draft { get; init; }
        [JsonPropertyName("prerelease")] public bool Prerelease { get; init; }
        [JsonPropertyName("published_at")] public DateTime? PublishedAt { get; init; }
        [JsonPropertyName("assets")] public List<GhAsset>? Assets { get; init; }
    }

    private sealed class GhAsset
    {
        [JsonPropertyName("name")] public string? Name { get; init; }
        [JsonPropertyName("browser_download_url")] public string? BrowserDownloadUrl { get; init; }
        [JsonPropertyName("size")] public long Size { get; init; }
    }
}
