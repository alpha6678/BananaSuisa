using Ribanense.Solucoes.App.Chocolatey.Services.Sources;
using Ribanense.Solucoes.App.Chocolatey.Tests.Helpers;
using Xunit;

namespace Ribanense.Solucoes.App.Chocolatey.Tests;

public class ChocolateySourceServiceTests
{
    [Fact]
    public void ParseListOutput_reads_limited_source_rows()
    {
        string stdout = """
            chocolatey|https://community.chocolatey.org/api/v2/|false|0
            internal|https://packages.example.local/|true|10
            """;

        var sources = ChocolateySourceService.ParseListOutput(stdout);

        Assert.Equal(2, sources.Count);
        Assert.Equal("chocolatey", sources[0].Name);
        Assert.False(sources[0].Disabled);
        Assert.True(sources[1].Disabled);
        Assert.Equal("10", sources[1].Priority);
    }

    [Fact]
    public async Task RemoveAsync_runs_source_remove_by_name()
    {
        var executor = new FakeChocolateyExecutor().Enqueue(0, "ok");
        var service = new ChocolateySourceService(executor);

        await service.RemoveAsync("internal", null, CancellationToken.None);

        Assert.Equal(["source", "remove", "--name", "internal"], executor.Calls[0]);
    }
}
