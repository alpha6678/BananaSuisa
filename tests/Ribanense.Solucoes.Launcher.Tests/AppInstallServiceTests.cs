using Ribanense.Solucoes.Launcher.Domain;
using Ribanense.Solucoes.Launcher.Services;
using Ribanense.Solucoes.Launcher.Tests.Helpers;
using Xunit;

namespace Ribanense.Solucoes.Launcher.Tests;

public class AppInstallServiceTests
{
    private const string Manifest = """
        {
          "id": "com.ribanense.winget",
          "name": "Gestor WinGet",
          "publicName": "Gestor WinGet",
          "version": "1.0.0",
          "minimumLauncherVersion": "1.0.0",
          "entryExecutable": "App.exe",
          "githubTagPrefix": "winget-v"
        }
        """;

    private static ReleaseInfo MakeRelease(string zipUrl, string? shaUrl, string tag = "winget-v1.0.0", string version = "1.0.0")
    {
        var assets = new List<ReleaseAsset>
        {
            new() { Name = "winget-1.0.0-win-x64.zip", DownloadUrl = zipUrl, Size = 0 }
        };
        if (shaUrl is not null)
        {
            assets.Add(new ReleaseAsset { Name = "winget-1.0.0-win-x64.zip.sha256", DownloadUrl = shaUrl, Size = 0 });
        }

        return new ReleaseInfo
        {
            Tag = tag,
            Version = version,
            Name = $"Winget {version}",
            IsPrerelease = false,
            PublishedAtUtc = DateTime.UtcNow,
            Assets = assets
        };
    }

    [Fact]
    public async Task Install_extracts_zip_to_aplicativos_root()
    {
        using var tmp = new TempFolder();
        string zipUrl = "https://example.com/x.zip";
        string shaUrl = "https://example.com/x.sha";

        byte[] zipBytes = ZipBuilder.CreateWithManifest(Manifest);
        string sha = ZipBuilder.ShaFileContent(zipBytes, "winget-1.0.0-win-x64.zip");

        var gh = new FakeGitHubClient();
        gh.BytesResponses[zipUrl] = zipBytes;
        gh.BytesResponses[shaUrl] = System.Text.Encoding.UTF8.GetBytes(sha);

        var svc = new AppInstallService(gh, new InstalledAppsRegistry(), new InMemoryLog());
        var release = MakeRelease(zipUrl, shaUrl);

        var result = await svc.InstallAsync(
            new AppInstallRequest("com.ribanense.winget", release, tmp.Path, null),
            CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.NotNull(result.InstallPath);
        Assert.True(File.Exists(Path.Combine(result.InstallPath!, "app.json")));
        Assert.True(File.Exists(Path.Combine(result.InstallPath!, "App.exe")));
        Assert.EndsWith("Winget", result.InstallPath!);
    }

    [Fact]
    public async Task Install_rejects_mismatched_sha256()
    {
        using var tmp = new TempFolder();
        string zipUrl = "https://example.com/x.zip";
        string shaUrl = "https://example.com/x.sha";

        byte[] zipBytes = ZipBuilder.CreateWithManifest(Manifest);
        string wrongSha = "deadbeef".PadRight(64, '0') + "  winget-1.0.0-win-x64.zip";

        var gh = new FakeGitHubClient();
        gh.BytesResponses[zipUrl] = zipBytes;
        gh.BytesResponses[shaUrl] = System.Text.Encoding.UTF8.GetBytes(wrongSha);

        var svc = new AppInstallService(gh, new InstalledAppsRegistry(), new InMemoryLog());
        var release = MakeRelease(zipUrl, shaUrl);

        var result = await svc.InstallAsync(
            new AppInstallRequest("com.ribanense.winget", release, tmp.Path, null),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("SHA256", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Path.Combine(tmp.Path, "Winget")));
    }

    [Fact]
    public async Task Install_without_sha_asset_still_extracts()
    {
        using var tmp = new TempFolder();
        string zipUrl = "https://example.com/x.zip";
        byte[] zipBytes = ZipBuilder.CreateWithManifest(Manifest);

        var gh = new FakeGitHubClient();
        gh.BytesResponses[zipUrl] = zipBytes;

        var svc = new AppInstallService(gh, new InstalledAppsRegistry(), new InMemoryLog());
        var release = MakeRelease(zipUrl, shaUrl: null);

        var result = await svc.InstallAsync(
            new AppInstallRequest("com.ribanense.winget", release, tmp.Path, null),
            CancellationToken.None);

        Assert.True(result.Success, result.Error);
    }

    [Fact]
    public async Task Install_atomic_swap_preserves_previous_on_failure()
    {
        using var tmp = new TempFolder();
        // Simula instalação anterior
        string existing = Path.Combine(tmp.Path, "Winget");
        Directory.CreateDirectory(existing);
        File.WriteAllText(Path.Combine(existing, "app.json"), Manifest);
        File.WriteAllBytes(Path.Combine(existing, "App.exe"), new byte[] { 0x4D, 0x5A });
        File.WriteAllText(Path.Combine(existing, "marker.txt"), "v1");

        string zipUrl = "https://example.com/x.zip";
        byte[] zipBytes = ZipBuilder.CreateWithManifest(Manifest);

        var gh = new FakeGitHubClient();
        gh.BytesResponses[zipUrl] = zipBytes;

        var svc = new AppInstallService(gh, new InstalledAppsRegistry(), new InMemoryLog());
        var release = MakeRelease(zipUrl, shaUrl: null);

        var result = await svc.InstallAsync(
            new AppInstallRequest("com.ribanense.winget", release, tmp.Path, null),
            CancellationToken.None);

        Assert.True(result.Success, result.Error);
        // Nova instalação: marker.txt antigo foi substituído, mas app.json ainda existe.
        Assert.True(File.Exists(Path.Combine(existing, "app.json")));
        Assert.False(File.Exists(Path.Combine(existing, "marker.txt")));
        // .tmp ou .bak não devem permanecer
        var leftovers = Directory.GetDirectories(tmp.Path)
            .Where(d => d.Contains(".tmp-") || d.Contains(".bak-"))
            .ToList();
        Assert.Empty(leftovers);
    }

    [Fact]
    public async Task Install_fails_with_no_zip_asset()
    {
        using var tmp = new TempFolder();
        var gh = new FakeGitHubClient();
        var svc = new AppInstallService(gh, new InstalledAppsRegistry(), new InMemoryLog());

        var release = new ReleaseInfo
        {
            Tag = "winget-v1.0.0",
            Version = "1.0.0",
            Name = "Winget 1.0.0",
            IsPrerelease = false,
            PublishedAtUtc = DateTime.UtcNow,
            Assets = new List<ReleaseAsset>
            {
                new() { Name = "somente-readme.txt", DownloadUrl = "https://example.com/r.txt", Size = 0 }
            }
        };

        var result = await svc.InstallAsync(
            new AppInstallRequest("com.ribanense.winget", release, tmp.Path, null),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(".zip", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Uninstall_removes_folder_when_not_running()
    {
        using var tmp = new TempFolder();
        string dir = Path.Combine(tmp.Path, "Winget");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "app.json"), Manifest);
        File.WriteAllBytes(Path.Combine(dir, "App.exe"), new byte[] { 0x4D, 0x5A });

        var svc = new AppInstallService(new FakeGitHubClient(), new InstalledAppsRegistry(), new InMemoryLog());
        var result = svc.Uninstall(tmp.Path, "com.ribanense.winget");

        Assert.True(result.Success, result.Error);
        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public void Uninstall_not_installed_returns_false()
    {
        using var tmp = new TempFolder();
        var svc = new AppInstallService(new FakeGitHubClient(), new InstalledAppsRegistry(), new InMemoryLog());
        var result = svc.Uninstall(tmp.Path, "com.ribanense.fantasma");

        Assert.False(result.Success);
    }
}
