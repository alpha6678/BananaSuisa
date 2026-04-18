using Ribanense.Solucoes.App.Winget.Domain;
using Ribanense.Solucoes.App.Winget.Services;
using Ribanense.Solucoes.App.Winget.Tests.Helpers;
using Xunit;

namespace Ribanense.Solucoes.App.Winget.Tests;

public class WingetListServiceTests
{
    private const string ListOutput = """
Name              Id                          Version   Available  Source
----              --                          -------   ---------  ------
Visual Studio Code Microsoft.VisualStudioCode  1.90.0    1.95.3     winget
7-Zip             7zip.7zip                    24.09                winget
""";

    [Fact]
    public async Task GetInstalledAsync_parses_output_and_flags_updates()
    {
        var fake = new FakeWingetExecutor
        {
            ForcedResponse = new WingetRunResult(0, ListOutput, "")
        };
        var svc = new WingetListService(fake);

        var installed = await svc.GetInstalledAsync(CancellationToken.None);

        Assert.Equal(2, installed.Count);

        var vsc = installed.Single(p => p.Id == "Microsoft.VisualStudioCode");
        Assert.Equal("1.90.0", vsc.InstalledVersion);
        Assert.Equal("1.95.3", vsc.AvailableVersion);
        Assert.True(vsc.HasUpdate);

        var sevenZip = installed.Single(p => p.Id == "7zip.7zip");
        Assert.Null(sevenZip.AvailableVersion);
        Assert.False(sevenZip.HasUpdate);
    }

    [Fact]
    public async Task GetInstalledAsync_uses_list_command_with_expected_args()
    {
        var fake = new FakeWingetExecutor
        {
            ForcedResponse = new WingetRunResult(0, ListOutput, "")
        };
        var svc = new WingetListService(fake);

        await svc.GetInstalledAsync(CancellationToken.None);

        Assert.Equal("list", fake.Calls[0][0]);
        Assert.Contains("--disable-interactivity", fake.Calls[0]);
    }
}
