using Ribanense.Solucoes.App.Chocolatey.Services;
using Ribanense.Solucoes.App.Chocolatey.Tests.Helpers;
using Xunit;

namespace Ribanense.Solucoes.App.Chocolatey.Tests;

public class ChocolateySearchServiceTests
{
    [Fact]
    public void ParseSearchOutput_reads_limited_pipe_output()
    {
        string stdout = """
            Chocolatey v2.2.0
            git|2.44.0
            vscode|1.90.0
            """;

        var packages = ChocolateySearchService.ParseSearchOutput(stdout);

        Assert.Equal(2, packages.Count);
        Assert.Equal("git", packages[0].Id);
        Assert.Equal("2.44.0", packages[0].Version);
        Assert.Equal("Chocolatey", packages[0].Source);
    }

    [Fact]
    public async Task SearchAsync_runs_choco_search_with_limit_output()
    {
        var executor = new FakeChocolateyExecutor()
            .Enqueue(0, "git|2.44.0");
        var service = new ChocolateySearchService(executor);

        var packages = await service.SearchAsync("git", CancellationToken.None);

        Assert.Single(packages);
        Assert.Equal(["search", "git", "--limit-output"], executor.Calls[0]);
    }

    [Fact]
    public async Task SearchAsync_returns_empty_for_blank_query()
    {
        var service = new ChocolateySearchService(new FakeChocolateyExecutor());

        var packages = await service.SearchAsync(" ", CancellationToken.None);

        Assert.Empty(packages);
    }
}
