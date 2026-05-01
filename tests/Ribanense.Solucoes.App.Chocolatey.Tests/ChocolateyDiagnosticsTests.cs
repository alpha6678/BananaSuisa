using Ribanense.Solucoes.App.Chocolatey.Services.Diagnostics;
using Ribanense.Solucoes.App.Chocolatey.Tests.Helpers;
using Xunit;

namespace Ribanense.Solucoes.App.Chocolatey.Tests;

public class ChocolateyDiagnosticsTests
{
    [Fact]
    public async Task InspectAsync_reports_missing_choco()
    {
        var diagnostics = new ChocolateyDiagnostics(
            new FakeChocolateyLocator(null),
            new FakeChocolateyExecutor());

        var status = await diagnostics.InspectAsync(CancellationToken.None);

        Assert.False(status.Found);
        Assert.False(status.Healthy);
    }

    [Fact]
    public async Task InspectAsync_reads_version_when_choco_exists()
    {
        var executor = new FakeChocolateyExecutor().Enqueue(0, "2.2.0");
        var diagnostics = new ChocolateyDiagnostics(
            new FakeChocolateyLocator(@"C:\ProgramData\chocolatey\bin\choco.exe"),
            executor);

        var status = await diagnostics.InspectAsync(CancellationToken.None);

        Assert.True(status.Healthy);
        Assert.Equal("2.2.0", status.Version);
        Assert.Equal(["--version"], executor.Calls[0]);
    }
}
