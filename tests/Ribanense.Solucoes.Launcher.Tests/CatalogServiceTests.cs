using Ribanense.Solucoes.Launcher.Services;
using Ribanense.Solucoes.Launcher.Tests.Helpers;
using Xunit;

namespace Ribanense.Solucoes.Launcher.Tests;

public class CatalogServiceTests
{
    private const string Url = "https://example.com/catalog.json";

    private const string ValidJson = """
        {
          "schemaVersion": 1,
          "apps": [
            {
              "id": "com.ribanense.winget",
              "name": "Gestor WinGet",
              "publicName": "Gestor WinGet",
              "description": "Instalar/atualizar/remover pacotes.",
              "category": "Pacotes",
              "githubOwner": "ribanense",
              "githubRepo": "RibanenseSolucoes",
              "githubTagPrefix": "winget-v",
              "minimumLauncherVersion": "1.0.0"
            }
          ]
        }
        """;

    [Fact]
    public async Task Parses_catalog_and_returns_apps()
    {
        var gh = new FakeGitHubClient();
        gh.StringResponses[Url] = ValidJson;
        var vault = new InMemoryVault();
        var log = new InMemoryLog();

        var svc = new CatalogService(gh, vault, log, Url);
        var catalog = await svc.GetCatalogAsync();

        Assert.Single(catalog.Apps);
        Assert.Equal("com.ribanense.winget", catalog.Apps[0].Id);
        Assert.Equal("winget-v", catalog.Apps[0].GithubTagPrefix);
        Assert.Equal(1, catalog.SchemaVersion);
    }

    [Fact]
    public async Task Uses_in_memory_cache_within_ttl()
    {
        var gh = new FakeGitHubClient();
        gh.StringResponses[Url] = ValidJson;
        var svc = new CatalogService(gh, new InMemoryVault(), new InMemoryLog(), Url);

        await svc.GetCatalogAsync();
        await svc.GetCatalogAsync();
        await svc.GetCatalogAsync();

        Assert.Single(gh.StringCalls);
    }

    [Fact]
    public async Task ForceRefresh_bypasses_cache()
    {
        var gh = new FakeGitHubClient();
        gh.StringResponses[Url] = ValidJson;
        var svc = new CatalogService(gh, new InMemoryVault(), new InMemoryLog(), Url);

        await svc.GetCatalogAsync();
        await svc.GetCatalogAsync(forceRefresh: true);

        Assert.Equal(2, gh.StringCalls.Count);
    }

    [Fact]
    public async Task Persists_and_reuses_vault_cache_when_offline()
    {
        var vault = new InMemoryVault();

        // Primeira chamada com rede OK
        var gh1 = new FakeGitHubClient();
        gh1.StringResponses[Url] = ValidJson;
        var svc1 = new CatalogService(gh1, vault, new InMemoryLog(), Url);
        await svc1.GetCatalogAsync();

        // Nova instância sem rede: deve ler do cache no vault
        var gh2 = new FakeGitHubClient();
        gh2.Failures[Url] = new System.Net.Http.HttpRequestException("offline");
        var svc2 = new CatalogService(gh2, vault, new InMemoryLog(), Url);

        var catalog = await svc2.GetCatalogAsync();
        Assert.Single(catalog.Apps);
        Assert.Equal("com.ribanense.winget", catalog.Apps[0].Id);
    }

    [Fact]
    public async Task Without_cache_rethrows_when_network_fails()
    {
        var gh = new FakeGitHubClient();
        gh.Failures[Url] = new System.Net.Http.HttpRequestException("offline");

        var svc = new CatalogService(gh, new InMemoryVault(), new InMemoryLog(), Url);

        await Assert.ThrowsAsync<System.Net.Http.HttpRequestException>(() => svc.GetCatalogAsync());
    }

    [Fact]
    public async Task Records_LastRefreshedAtUtc_on_success()
    {
        var gh = new FakeGitHubClient();
        gh.StringResponses[Url] = ValidJson;
        var svc = new CatalogService(gh, new InMemoryVault(), new InMemoryLog(), Url);

        DateTime before = DateTime.UtcNow;
        await svc.GetCatalogAsync();
        DateTime after = DateTime.UtcNow;

        var at = svc.LastRefreshedAtUtc;
        Assert.NotNull(at);
        Assert.InRange(at!.Value, before.AddSeconds(-1), after.AddSeconds(1));
    }

    [Fact]
    public void Ctor_null_args_throw()
    {
        var gh = new FakeGitHubClient();
        var vault = new InMemoryVault();
        var log = new InMemoryLog();

        Assert.Throws<ArgumentNullException>(() => new CatalogService(null!, vault, log, Url));
        Assert.Throws<ArgumentNullException>(() => new CatalogService(gh, null!, log, Url));
        Assert.Throws<ArgumentNullException>(() => new CatalogService(gh, vault, null!, Url));
        Assert.Throws<ArgumentException>(() => new CatalogService(gh, vault, log, ""));
    }
}
