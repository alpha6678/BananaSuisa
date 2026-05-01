using Ribanense.Solucoes.App.Chocolatey.Services;
using Ribanense.Solucoes.App.Chocolatey.Tests.Helpers;
using Xunit;

namespace Ribanense.Solucoes.App.Chocolatey.Tests;

public class ChocolateyInstallServiceTests
{
    [Theory]
    [InlineData("install")]
    [InlineData("upgrade")]
    [InlineData("uninstall")]
    public async Task Operations_run_choco_with_confirm_and_no_progress(string verb)
    {
        var executor = new FakeChocolateyExecutor().Enqueue(0, "ok");
        var service = new ChocolateyInstallService(executor);

        switch (verb)
        {
            case "install":
                await service.InstallAsync("git", null, CancellationToken.None);
                break;
            case "upgrade":
                await service.UpgradeAsync("git", null, CancellationToken.None);
                break;
            case "uninstall":
                await service.UninstallAsync("git", null, CancellationToken.None);
                break;
        }

        Assert.Equal([verb, "git", "-y", "--no-progress"], executor.Calls[0]);
    }

    [Fact]
    public async Task InstallAsync_rejects_blank_package_id()
    {
        var service = new ChocolateyInstallService(new FakeChocolateyExecutor());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.InstallAsync(" ", null, CancellationToken.None));
    }
}
