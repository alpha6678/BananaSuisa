using Ribanense.Solucoes.Launcher.Services;
using Ribanense.Solucoes.Launcher.Tests.Helpers;
using Xunit;

namespace Ribanense.Solucoes.Launcher.Tests;

public class CatalogServiceBundledTests
{
    private const string Url = "https://example.com/catalog.json";

    private const string BundledJson = """
        {
          "schemaVersion": 1,
          "apps": [
            {
              "id": "com.ribanense.winget",
              "name": "Gestor WinGet",
              "description": "Bundled.",
              "category": "Pacotes",
              "githubOwner": "OWNER",
              "githubRepo": "RibanenseSolucoes",
              "githubTagPrefix": "winget-v",
              "minimumLauncherVersion": "1.0.0"
            }
          ]
        }
        """;

    [Fact]
    public async Task Falls_back_to_bundled_when_network_and_vault_empty()
    {
        var gh = new FakeGitHubClient();
        gh.Failures[Url] = new System.Net.Http.HttpRequestException("offline");

        var svc = new CatalogService(gh, new InMemoryVault(), new InMemoryLog(), Url, BundledJson);

        var catalog = await svc.GetCatalogAsync();

        Assert.Single(catalog.Apps);
        Assert.Equal("com.ribanense.winget", catalog.Apps[0].Id);
    }

    [Fact]
    public async Task Vault_cache_takes_precedence_over_bundled()
    {
        var vault = new InMemoryVault();

        // 1. Rede online preenche o cache
        var ghOk = new FakeGitHubClient();
        ghOk.StringResponses[Url] = """{"schemaVersion":1,"apps":[{"id":"com.ribanense.uwp","name":"Uwp","publicName":"Uwp","description":"","category":"","githubOwner":"O","githubRepo":"R","githubTagPrefix":"uwp-v","minimumLauncherVersion":"1.0.0"}]}""";
        var svc1 = new CatalogService(ghOk, vault, new InMemoryLog(), Url, BundledJson);
        await svc1.GetCatalogAsync();

        // 2. Nova instancia offline: deve pegar do vault, nao do bundled
        var ghFail = new FakeGitHubClient();
        ghFail.Failures[Url] = new System.Net.Http.HttpRequestException("offline");
        var svc2 = new CatalogService(ghFail, vault, new InMemoryLog(), Url, BundledJson);

        var catalog = await svc2.GetCatalogAsync();
        Assert.Single(catalog.Apps);
        Assert.Equal("com.ribanense.uwp", catalog.Apps[0].Id);
    }

    [Fact]
    public async Task Without_bundled_still_rethrows_when_fully_offline()
    {
        var gh = new FakeGitHubClient();
        gh.Failures[Url] = new System.Net.Http.HttpRequestException("offline");

        var svc = new CatalogService(gh, new InMemoryVault(), new InMemoryLog(), Url, bundledJson: null);

        await Assert.ThrowsAsync<System.Net.Http.HttpRequestException>(() => svc.GetCatalogAsync());
    }

    [Fact]
    public async Task Invalid_bundled_is_ignored_and_rethrows()
    {
        var gh = new FakeGitHubClient();
        gh.Failures[Url] = new System.Net.Http.HttpRequestException("offline");

        var svc = new CatalogService(gh, new InMemoryVault(), new InMemoryLog(), Url, bundledJson: "bundled-json-lixo");

        await Assert.ThrowsAsync<System.Net.Http.HttpRequestException>(() => svc.GetCatalogAsync());
    }
}
