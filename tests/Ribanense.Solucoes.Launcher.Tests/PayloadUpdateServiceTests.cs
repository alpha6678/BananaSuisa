using Ribanense.Solucoes.Launcher.Domain;
using Ribanense.Solucoes.Launcher.Services;
using Ribanense.Solucoes.Launcher.Tests.Helpers;
using Ribanense.Solucoes.PluginSDK.Payload;
using Xunit;

namespace Ribanense.Solucoes.Launcher.Tests;

public class PayloadUpdateServiceTests
{
    [Fact]
    public async Task Stage_uses_delta_chain_and_keeps_unchanged_file()
    {
        using var installed = new TempFolder();
        using var v2 = new TempFolder();
        using var staging = new TempFolder();

        File.WriteAllText(Path.Combine(installed.Path, "fica.txt"), "igual");
        File.WriteAllText(Path.Combine(installed.Path, "muda.txt"), "v1");
        var from = PayloadManifest.Compute(installed.Path, "1.0.0", "App.exe");
        from.Save(Path.Combine(installed.Path, PayloadManifest.FileName));

        File.WriteAllText(Path.Combine(v2.Path, "fica.txt"), "igual");
        File.WriteAllText(Path.Combine(v2.Path, "muda.txt"), "v2");
        var to = PayloadManifest.Compute(v2.Path, "1.0.1", "App.exe");
        to.Save(Path.Combine(v2.Path, PayloadManifest.FileName));
        var delta = DeltaManifest.Diff(from, to);
        byte[] deltaZip = PayloadApplier.CreateDeltaZip(v2.Path, delta);

        var gh = new FakeGitHubClient();
        gh.BytesResponses["https://example.com/delta.zip"] = deltaZip;
        gh.StringResponses["https://example.com/delta.json"] = delta.Serialize();

        var release = new ReleaseInfo
        {
            Tag = "app-v1.0.1",
            Version = "1.0.1",
            Name = "App 1.0.1",
            IsPrerelease = false,
            PublishedAtUtc = DateTime.UtcNow,
            Assets = new[]
            {
                new ReleaseAsset { Name = "app-1.0.1-win-x64.zip", DownloadUrl = "https://example.com/full.zip", Size = 1 },
                new ReleaseAsset { Name = "delta-from-1.0.0.zip", DownloadUrl = "https://example.com/delta.zip", Size = deltaZip.Length },
                new ReleaseAsset { Name = "delta-from-1.0.0.json", DownloadUrl = "https://example.com/delta.json", Size = 10 }
            }
        };

        var svc = new PayloadUpdateService(gh, new InMemoryLog());
        var result = await svc.StageAsync(installed.Path, "1.0.0", release, new[] { release }, staging.Path, null, CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.Equal(PayloadStageKind.Chain, result.Kind);
        Assert.Equal("igual", File.ReadAllText(Path.Combine(staging.Path, "fica.txt")));
        Assert.Equal("v2", File.ReadAllText(Path.Combine(staging.Path, "muda.txt")));
        Assert.DoesNotContain(gh.BytesCalls, url => url.Contains("full.zip"));
    }

    [Fact]
    public async Task Stage_falls_back_to_full_zip_when_delta_is_missing()
    {
        using var staging = new TempFolder();
        byte[] zip = ZipBuilder.CreateWithManifest("""
            {
              "id": "com.ribanense.winget",
              "name": "Gestor WinGet",
              "publicName": "Gestor WinGet",
              "version": "1.0.0",
              "minimumLauncherVersion": "1.0.0",
              "entryExecutable": "App.exe",
              "githubTagPrefix": "winget-v"
            }
            """);

        var gh = new FakeGitHubClient();
        gh.BytesResponses["https://example.com/full.zip"] = zip;
        var release = new ReleaseInfo
        {
            Tag = "winget-v1.0.0",
            Version = "1.0.0",
            Name = "Winget 1.0.0",
            IsPrerelease = false,
            PublishedAtUtc = DateTime.UtcNow,
            Assets = new[]
            {
                new ReleaseAsset { Name = "winget-1.0.0-win-x64.zip", DownloadUrl = "https://example.com/full.zip", Size = zip.Length }
            }
        };

        var svc = new PayloadUpdateService(gh, new InMemoryLog());
        var result = await svc.StageAsync(null, null, release, Array.Empty<ReleaseInfo>(), staging.Path, null, CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.Equal(PayloadStageKind.Full, result.Kind);
        Assert.True(File.Exists(Path.Combine(staging.Path, "app.json")));
    }

    [Fact]
    public void Release_zip_asset_ignores_delta_packages()
    {
        var release = new ReleaseInfo
        {
            Tag = "app-v1.0.1",
            Version = "1.0.1",
            Name = "App",
            IsPrerelease = false,
            PublishedAtUtc = DateTime.UtcNow,
            Assets = new[]
            {
                new ReleaseAsset { Name = "delta-from-1.0.0.zip", DownloadUrl = "https://example.com/d.zip", Size = 1 },
                new ReleaseAsset { Name = "app-1.0.1-win-x64.zip", DownloadUrl = "https://example.com/full.zip", Size = 2 },
                new ReleaseAsset { Name = "app-1.0.1-win-x64.zip.sha256", DownloadUrl = "https://example.com/full.sha", Size = 3 }
            }
        };

        Assert.Equal("app-1.0.1-win-x64.zip", release.ZipAsset!.Name);
        Assert.Equal("app-1.0.1-win-x64.zip.sha256", release.Sha256Asset!.Name);
        Assert.Equal(new[] { "1.0.0" }, release.DeltaFromVersions);
    }

    [Fact]
    public void Merge_replaces_same_id_and_appends_partners()
    {
        var catalog = new CatalogDocument
        {
            Apps = new List<CatalogEntry>
            {
                new() { Id = "com.ribanense.mago", Name = "Velho", GithubOwner = "alpha6678", GithubRepo = "BananaSuisa", GithubTagPrefix = "mago-v" },
                new() { Id = "com.ribanense.winget", Name = "WinGet", GithubOwner = "alpha6678", GithubRepo = "BananaSuisa", GithubTagPrefix = "winget-v" }
            }
        };
        var partners = new PartnerCatalogDocument
        {
            Partners = new List<CatalogEntry>
            {
                new()
                {
                    Id = "com.ribanense.mago",
                    Name = "Mago",
                    GithubOwner = "Nickallafe",
                    GithubRepo = "Mago",
                    GithubTagPrefix = "v"
                }
            }
        };

        var merged = CatalogService.Merge(catalog, partners);

        Assert.Equal(2, merged.Apps.Count);
        Assert.Equal("Nickallafe", merged.Apps[0].GithubOwner);
        Assert.True(merged.Apps[0].IsPartner);
        Assert.False(merged.Apps[1].IsPartner);
    }
}
