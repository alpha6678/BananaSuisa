using Ribanense.Solucoes.App.Winget.Domain;
using Ribanense.Solucoes.App.Winget.Services;
using Ribanense.Solucoes.App.Winget.Tests.Helpers;
using Xunit;

namespace Ribanense.Solucoes.App.Winget.Tests;

public class WingetSearchServiceTests
{
    private const string SampleOutput = """
Name              Id                          Version   Source
----              --                          -------   ------
Visual Studio Code Microsoft.VisualStudioCode  1.95.3    winget
7-Zip             7zip.7zip                    24.09     winget
""";

    [Fact]
    public async Task SearchAsync_empty_query_returns_empty_without_calling_executor()
    {
        var fake = new FakeWingetExecutor();
        var svc = new WingetSearchService(fake);

        var result = await svc.SearchAsync("", CancellationToken.None);

        Assert.Empty(result);
        Assert.Empty(fake.Calls);
    }

    [Fact]
    public async Task SearchAsync_runs_winget_with_expected_args()
    {
        var fake = new FakeWingetExecutor
        {
            ForcedResponse = new WingetRunResult(0, SampleOutput, "")
        };
        var svc = new WingetSearchService(fake);

        await svc.SearchAsync("vscode", CancellationToken.None);

        Assert.Single(fake.Calls);
        var args = fake.Calls[0];
        Assert.Equal("search", args[0]);
        Assert.Equal("vscode", args[1]);
        Assert.Contains("--disable-interactivity", args);
        Assert.Contains("--accept-source-agreements", args);
    }

    [Fact]
    public async Task SearchAsync_parses_rows_into_WingetPackage()
    {
        var fake = new FakeWingetExecutor
        {
            ForcedResponse = new WingetRunResult(0, SampleOutput, "")
        };
        var svc = new WingetSearchService(fake);

        var packages = await svc.SearchAsync("vs", CancellationToken.None);

        Assert.Equal(2, packages.Count);
        Assert.Equal("Microsoft.VisualStudioCode", packages[0].Id);
        Assert.Equal("1.95.3", packages[0].Version);
        Assert.Equal("winget", packages[0].Source);
    }

    [Fact]
    public async Task SearchAsync_empty_stdout_with_nonzero_exit_returns_empty()
    {
        var fake = new FakeWingetExecutor
        {
            ForcedResponse = new WingetRunResult(-2147023728, "", "No package found.")
        };
        var svc = new WingetSearchService(fake);

        var packages = await svc.SearchAsync("xpto", CancellationToken.None);

        Assert.Empty(packages);
    }
}
