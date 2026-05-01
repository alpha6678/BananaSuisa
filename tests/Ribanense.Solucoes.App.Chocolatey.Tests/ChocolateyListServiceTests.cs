using Ribanense.Solucoes.App.Chocolatey.Services;
using Ribanense.Solucoes.App.Chocolatey.Tests.Helpers;
using Xunit;

namespace Ribanense.Solucoes.App.Chocolatey.Tests;

public class ChocolateyListServiceTests
{
    [Fact]
    public void ParseListOutput_adds_available_versions_from_outdated()
    {
        string installed = """
            git|2.43.0
            vscode|1.90.0
            """;
        var updates = ChocolateyListService.ParseOutdatedOutput("git|2.43.0|2.44.0|false");

        var packages = ChocolateyListService.ParseListOutput(installed, updates);

        Assert.Equal(2, packages.Count);
        Assert.Equal("2.44.0", packages[0].AvailableVersion);
        Assert.True(packages[0].HasUpdate);
        Assert.False(packages[1].HasUpdate);
    }

    [Fact]
    public void ParseOutdatedOutput_reads_package_current_available()
    {
        string stdout = """
            git|2.43.0|2.44.0|false
            nodejs|20.0.0|22.0.0|false
            """;

        var updates = ChocolateyListService.ParseOutdatedOutput(stdout);

        Assert.Equal("2.44.0", updates["git"]);
        Assert.Equal("22.0.0", updates["nodejs"]);
    }

    [Fact]
    public async Task GetInstalledAsync_runs_list_and_outdated()
    {
        var executor = new FakeChocolateyExecutor()
            .Enqueue(0, "git|2.43.0")
            .Enqueue(0, "git|2.43.0|2.44.0|false");
        var service = new ChocolateyListService(executor);

        var packages = await service.GetInstalledAsync(CancellationToken.None);

        Assert.Single(packages);
        Assert.Equal(["list", "--local-only", "--limit-output"], executor.Calls[0]);
        Assert.Equal(["outdated", "--limit-output"], executor.Calls[1]);
    }
}
