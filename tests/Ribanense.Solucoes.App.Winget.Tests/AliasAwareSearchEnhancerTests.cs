using Ribanense.Solucoes.App.Winget.Domain;
using Ribanense.Solucoes.App.Winget.Services;
using Ribanense.Solucoes.App.Winget.Services.Search;
using Ribanense.Solucoes.App.Winget.Tests.Helpers;
using Xunit;

namespace Ribanense.Solucoes.App.Winget.Tests;

public class AliasAwareSearchEnhancerTests
{
    private static AppAlias VsCode() => new()
    {
        Id = "Microsoft.VisualStudioCode",
        PublicName = "Visual Studio Code",
        Synonyms = new List<string> { "code", "vs code", "vscode", "editor de codigo" }
    };

    private static AppAlias Chrome() => new()
    {
        Id = "Google.Chrome",
        PublicName = "Google Chrome",
        Synonyms = new List<string> { "chrome", "google chrome", "navegador google" }
    };

    [Fact]
    public async Task Empty_query_returns_empty()
    {
        var enhancer = new AliasAwareSearchEnhancer(new FakeWingetSearchService(), new InMemoryAliasCatalog());
        var result = await enhancer.SearchAsync("", CancellationToken.None);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Exact_synonym_match_boosts_curated_id_to_top()
    {
        var search = new FakeWingetSearchService();
        search.ByQuery["Microsoft.VisualStudioCode"] = new()
        {
            new WingetPackage("Visual Studio Code", "Microsoft.VisualStudioCode", "1.95.0", "winget")
        };
        search.ByQuery["code"] = new()
        {
            new WingetPackage("Some Other Editor", "Other.CodeEditor", "1.0", "winget")
        };

        var enhancer = new AliasAwareSearchEnhancer(search, new InMemoryAliasCatalog(VsCode()));

        var result = await enhancer.SearchAsync("code", CancellationToken.None);

        Assert.NotEmpty(result);
        Assert.Equal("Microsoft.VisualStudioCode", result[0].Id);
    }

    [Fact]
    public async Task Fuzzy_typo_resolves_to_curated()
    {
        var search = new FakeWingetSearchService();
        search.ByQuery["Google.Chrome"] = new()
        {
            new WingetPackage("Google Chrome", "Google.Chrome", "121.0", "winget")
        };
        // "chorme" (typo de "chrome") nao retornara nada pelo search, mas via
        // alias fuzzy deve resolver para Google.Chrome.
        var enhancer = new AliasAwareSearchEnhancer(search, new InMemoryAliasCatalog(Chrome()));

        var result = await enhancer.SearchAsync("chorme", CancellationToken.None);

        Assert.Contains(result, p => p.Id == "Google.Chrome");
        Assert.Contains(search.Calls, c => c == "Google.Chrome");
    }

    [Fact]
    public async Task No_alias_match_falls_back_to_raw_query()
    {
        var search = new FakeWingetSearchService();
        search.ByQuery["coisa obscura"] = new()
        {
            new WingetPackage("Coisa Obscura", "Obscuro.Coisa", "1.0", "winget")
        };
        var enhancer = new AliasAwareSearchEnhancer(search, new InMemoryAliasCatalog(VsCode()));

        var result = await enhancer.SearchAsync("coisa obscura", CancellationToken.None);

        Assert.Contains(result, p => p.Id == "Obscuro.Coisa");
        Assert.Contains(search.Calls, c => c == "coisa obscura");
    }

    [Fact]
    public async Task Dedup_by_id_preserves_first_occurrence()
    {
        var search = new FakeWingetSearchService();
        var pkg = new WingetPackage("VS Code", "Microsoft.VisualStudioCode", "1.95.0", "winget");
        search.ByQuery["Microsoft.VisualStudioCode"] = new() { pkg };
        search.ByQuery["code"] = new() { pkg }; // duplicata

        var enhancer = new AliasAwareSearchEnhancer(search, new InMemoryAliasCatalog(VsCode()));
        var result = await enhancer.SearchAsync("code", CancellationToken.None);

        Assert.Single(result);
    }

    [Fact]
    public async Task ResolveCuratedMatches_exact_takes_precedence_over_fuzzy()
    {
        var vscode = VsCode();
        var chrome = Chrome();
        var enhancer = new AliasAwareSearchEnhancer(
            new FakeWingetSearchService(),
            new InMemoryAliasCatalog(vscode, chrome));

        // "code" casa exatamente com sinonimo de VsCode.
        var matches = enhancer.ResolveCuratedMatches(Similarity.Normalize("code"));
        Assert.NotEmpty(matches);
        Assert.Equal("Microsoft.VisualStudioCode", matches[0].Id);
    }

    [Fact]
    public async Task Null_args_throw_in_ctor()
    {
        Assert.Throws<ArgumentNullException>(() => new AliasAwareSearchEnhancer(null!, new InMemoryAliasCatalog()));
        Assert.Throws<ArgumentNullException>(() => new AliasAwareSearchEnhancer(new FakeWingetSearchService(), null!));
        await Task.CompletedTask;
    }
}
