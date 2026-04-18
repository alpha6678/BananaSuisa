using Ribanense.Solucoes.Launcher.Domain;
using Ribanense.Solucoes.Launcher.Services;
using Ribanense.Solucoes.Launcher.Tests.Helpers;
using Xunit;

namespace Ribanense.Solucoes.Launcher.Tests;

public class ReleaseCheckServiceTests
{
    private const string Owner = "ribanense";
    private const string Repo = "RibanenseSolucoes";
    private const string Prefix = "winget-v";
    private const string Url = "https://api.github.com/repos/ribanense/RibanenseSolucoes/releases?per_page=100";

    private const string SampleJson = """
        [
          {
            "tag_name": "winget-v1.0.0",
            "name": "Gestor WinGet 1.0.0",
            "draft": false,
            "prerelease": false,
            "published_at": "2026-04-01T00:00:00Z",
            "assets": [
              { "name": "winget-1.0.0-win-x64.zip", "browser_download_url": "https://example.com/w1.zip", "size": 1024 },
              { "name": "winget-1.0.0-win-x64.zip.sha256", "browser_download_url": "https://example.com/w1.sha256", "size": 64 },
              { "name": "app.json", "browser_download_url": "https://example.com/w1.app.json", "size": 256 }
            ]
          },
          {
            "tag_name": "winget-v1.1.0",
            "name": "Gestor WinGet 1.1.0",
            "draft": false,
            "prerelease": false,
            "published_at": "2026-04-10T00:00:00Z",
            "assets": [
              { "name": "winget-1.1.0-win-x64.zip", "browser_download_url": "https://example.com/w11.zip", "size": 2048 }
            ]
          },
          {
            "tag_name": "winget-v1.2.0-beta.1",
            "name": "Gestor WinGet 1.2.0 beta 1",
            "draft": false,
            "prerelease": true,
            "published_at": "2026-04-15T00:00:00Z",
            "assets": []
          },
          {
            "tag_name": "uwp-v1.0.0",
            "name": "Uwp 1.0.0",
            "draft": false,
            "prerelease": false,
            "published_at": "2026-04-02T00:00:00Z",
            "assets": []
          },
          {
            "tag_name": "winget-v0.9.0-rascunho",
            "name": "rascunho",
            "draft": true,
            "prerelease": false,
            "published_at": null,
            "assets": []
          }
        ]
        """;

    private static ReleaseCheckService ServiceWithFake(out FakeGitHubClient gh)
    {
        gh = new FakeGitHubClient();
        gh.StringResponses[Url] = SampleJson;
        return new ReleaseCheckService(gh);
    }

    [Fact]
    public async Task GetAllReleases_filters_prefix_and_skips_drafts()
    {
        var svc = ServiceWithFake(out _);
        var all = await svc.GetAllReleasesAsync(Owner, Repo, Prefix, CancellationToken.None);

        // Exclui uwp-* e o rascunho de winget-v0.9.0
        Assert.All(all, r => Assert.StartsWith("winget-v", r.Tag));
        Assert.DoesNotContain(all, r => r.Tag == "winget-v0.9.0-rascunho");
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task GetLatestRelease_picks_highest_stable_by_default()
    {
        var svc = ServiceWithFake(out _);
        var latest = await svc.GetLatestReleaseAsync(Owner, Repo, Prefix, includePrerelease: false, CancellationToken.None);

        Assert.NotNull(latest);
        Assert.Equal("winget-v1.1.0", latest!.Tag);
        Assert.Equal("1.1.0", latest.Version);
        Assert.False(latest.IsPrerelease);
    }

    [Fact]
    public async Task GetLatestRelease_includePrerelease_picks_beta()
    {
        var svc = ServiceWithFake(out _);
        var latest = await svc.GetLatestReleaseAsync(Owner, Repo, Prefix, includePrerelease: true, CancellationToken.None);

        Assert.NotNull(latest);
        Assert.Equal("winget-v1.2.0-beta.1", latest!.Tag);
        Assert.True(latest.IsPrerelease);
    }

    [Fact]
    public async Task Assets_are_classified_by_helpers()
    {
        var svc = ServiceWithFake(out _);
        var all = await svc.GetAllReleasesAsync(Owner, Repo, Prefix, CancellationToken.None);

        var v1 = all.Single(r => r.Tag == "winget-v1.0.0");
        Assert.NotNull(v1.ZipAsset);
        Assert.NotNull(v1.Sha256Asset);
        Assert.NotNull(v1.ManifestAsset);

        var v11 = all.Single(r => r.Tag == "winget-v1.1.0");
        Assert.NotNull(v11.ZipAsset);
        Assert.Null(v11.Sha256Asset);
    }

    [Theory]
    [InlineData(null, "1.0.0", UpdateStatus.NotInstalled)]
    [InlineData("", "1.0.0", UpdateStatus.NotInstalled)]
    [InlineData("1.0.0", null, UpdateStatus.ReleaseNotFound)]
    [InlineData("1.0.0", "1.0.0", UpdateStatus.UpToDate)]
    [InlineData("1.0.0", "1.1.0", UpdateStatus.UpdateAvailable)]
    [InlineData("2.0.0", "1.9.9", UpdateStatus.UpToDate)]
    [InlineData("x", "1.0.0", UpdateStatus.CorruptedInstallation)]
    public void CompareVersions_classifies(string? installed, string? latest, UpdateStatus expected)
    {
        var svc = new ReleaseCheckService(new FakeGitHubClient());
        Assert.Equal(expected, svc.CompareVersions(installed, latest));
    }

    [Fact]
    public async Task Empty_args_throw()
    {
        var svc = new ReleaseCheckService(new FakeGitHubClient());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.GetAllReleasesAsync("", "r", "p", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.GetAllReleasesAsync("o", "", "p", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.GetAllReleasesAsync("o", "r", "", CancellationToken.None));
    }
}
