using Ribanense.Solucoes.Launcher.Domain;
using Ribanense.Solucoes.Launcher.Services;
using Ribanense.Solucoes.Launcher.Tests.Helpers;
using Ribanense.Solucoes.Launcher.ViewModels;
using Ribanense.Solucoes.PluginSDK.Manifest;
using Xunit;

namespace Ribanense.Solucoes.Launcher.Tests;

public class CatalogPendingUpdatesTests
{
    private const string Url = "https://example.com/catalog.json";
    private const string ReleasesUrl = "https://api.github.com/repos/ribanense/RibanenseSolucoes/releases?per_page=100";

    private const string CatalogJson = """
        {
          "schemaVersion": 1,
          "apps": [
            {
              "id": "com.ribanense.winget",
              "name": "Gestor WinGet",
              "publicName": "Gestor WinGet",
              "description": "Pacotes.",
              "category": "Pacotes",
              "githubOwner": "ribanense",
              "githubRepo": "RibanenseSolucoes",
              "githubTagPrefix": "winget-v",
              "minimumLauncherVersion": "1.0.0"
            },
            {
              "id": "com.ribanense.chocolatey",
              "name": "Gestor Chocolatey",
              "publicName": "Gestor Chocolatey",
              "description": "Pacotes.",
              "category": "Pacotes",
              "githubOwner": "ribanense",
              "githubRepo": "RibanenseSolucoes",
              "githubTagPrefix": "choco-v",
              "minimumLauncherVersion": "1.0.0"
            }
          ]
        }
        """;

    private const string WingetReleasesJson = """
        [
          {
            "tag_name": "winget-v1.2.0",
            "name": "winget-v1.2.0",
            "draft": false,
            "prerelease": false,
            "published_at": "2026-01-02T00:00:00Z",
            "assets": [{ "name": "winget.zip", "browser_download_url": "https://example.com/w.zip", "size": 10 }]
          }
        ]
        """;

    private const string ChocoReleasesJson = """
        [
          {
            "tag_name": "choco-v2.0.0",
            "name": "choco-v2.0.0",
            "draft": false,
            "prerelease": false,
            "published_at": "2026-01-02T00:00:00Z",
            "assets": [{ "name": "choco.zip", "browser_download_url": "https://example.com/c.zip", "size": 10 }]
          }
        ]
        """;

    [Fact]
    public async Task Only_installed_apps_with_newer_release_are_pending()
    {
        using var folder = new TempFolder();
        SeedInstalledApp(folder, "com.ribanense.winget", "1.0.0");

        var viewModel = Build(folder);
        await viewModel.LoadAsync(forceRefresh: true);
        await WaitForReleasesAsync(viewModel);

        // winget: instalado em 1.0.0, release 1.2.0 → pendente.
        // chocolatey: não instalado → fora da lista de atualizações.
        AppCardViewModel pending = Assert.Single(viewModel.PendingUpdates);
        Assert.Equal("com.ribanense.winget", pending.Id);
        Assert.True(viewModel.HasPendingUpdates);
        Assert.Equal("1 atualização disponível.", viewModel.UpdatesSummary);
    }

    [Fact]
    public async Task Installed_app_on_latest_version_leaves_the_list_empty()
    {
        using var folder = new TempFolder();
        SeedInstalledApp(folder, "com.ribanense.winget", "1.2.0");

        var viewModel = Build(folder);
        await viewModel.LoadAsync(forceRefresh: true);
        await WaitForReleasesAsync(viewModel);

        Assert.Empty(viewModel.PendingUpdates);
        Assert.False(viewModel.HasPendingUpdates);
        Assert.Equal("Todos os apps instalados estão na versão mais recente.", viewModel.UpdatesSummary);
    }

    [Fact]
    public async Task Summary_pluralizes_with_more_than_one_pending_update()
    {
        using var folder = new TempFolder();
        SeedInstalledApp(folder, "com.ribanense.winget", "1.0.0");
        SeedInstalledApp(folder, "com.ribanense.chocolatey", "1.0.0");

        var viewModel = Build(folder);
        await viewModel.LoadAsync(forceRefresh: true);
        await WaitForReleasesAsync(viewModel);

        Assert.Equal(2, viewModel.PendingUpdates.Count);
        Assert.Equal("2 atualizações disponíveis.", viewModel.UpdatesSummary);
    }

    private static CatalogViewModel Build(TempFolder folder)
    {
        var github = new FakeGitHubClient();
        github.StringResponses[Url] = CatalogJson;
        // Os dois apps consultam a mesma URL de releases; o filtro por prefixo de tag
        // é feito depois, na desserialização.
        github.StringResponses[ReleasesUrl] = MergeReleases(WingetReleasesJson, ChocoReleasesJson);

        var catalog = new CatalogService(github, new InMemoryVault(), new InMemoryLog(), Url);
        var releases = new ReleaseCheckService(github);
        var registry = new InstalledAppsRegistry();

        return new CatalogViewModel(
            catalog, releases, registry, new NoopHost(), new InMemoryLog(),
            folder.Sub("aplicativos"));
    }

    private static string MergeReleases(string winget, string choco)
    {
        var wingetItems = System.Text.Json.JsonDocument.Parse(winget).RootElement;
        var chocoItems = System.Text.Json.JsonDocument.Parse(choco).RootElement;
        var merged = wingetItems.EnumerateArray().Concat(chocoItems.EnumerateArray()).ToList();
        return System.Text.Json.JsonSerializer.Serialize(merged);
    }

    private static void SeedInstalledApp(TempFolder folder, string appId, string version)
    {
        string slug = appId.Split('.').Last();
        string dir = folder.Sub("aplicativos", slug);
        File.WriteAllText(Path.Combine(dir, "App.exe"), "binario");
        var manifest = new AppManifest
        {
            Id = appId,
            Name = appId,
            PublicName = appId,
            Version = version,
            MinimumLauncherVersion = "1.0.0",
            EntryExecutable = "App.exe",
            Category = "Teste",
            GithubTagPrefix = slug + "-v"
        };
        File.WriteAllText(Path.Combine(dir, "app.json"), manifest.Serialize());
    }

    private static async Task WaitForReleasesAsync(CatalogViewModel viewModel)
    {
        // Cada cartão busca as releases de forma assíncrona logo após entrar na lista.
        for (int i = 0; i < 200 && viewModel.Cards.Any(c => c.LatestRelease is null
                 && c.Status != UpdateStatus.ReleaseNotFound); i++)
        {
            await Task.Delay(10);
        }
    }

    private sealed class NoopHost : IAppCardHost
    {
        public Task InstallAsync(AppCardViewModel card) => Task.CompletedTask;
        public Task InstallRegisteredAsync(AppCardViewModel card) => Task.CompletedTask;
        public Task RegisterAsync(AppCardViewModel card) => Task.CompletedTask;
        public Task UpdateAsync(AppCardViewModel card) => Task.CompletedTask;
        public Task UninstallAsync(AppCardViewModel card) => Task.CompletedTask;
        public void Open(AppCardViewModel card) { }
        public Task InstallRuntimeAsync(AppCardViewModel card) => Task.CompletedTask;
    }
}
