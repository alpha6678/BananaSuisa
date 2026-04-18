using Ribanense.Solucoes.App.Winget.Domain;
using Ribanense.Solucoes.App.Winget.Services;
using Ribanense.Solucoes.App.Winget.Tests.Helpers;
using Xunit;

namespace Ribanense.Solucoes.App.Winget.Tests;

public class WingetInstallServiceTests
{
    [Fact]
    public async Task InstallAsync_uses_install_verb_and_package_id()
    {
        var fake = new FakeWingetExecutor
        {
            ForcedResponse = new WingetRunResult(0, "Installed OK", "")
        };
        var svc = new WingetInstallService(fake);

        var result = await svc.InstallAsync("Microsoft.VisualStudioCode", null, CancellationToken.None);

        Assert.True(result.Success);
        var args = fake.Calls[0];
        Assert.Equal("install", args[0]);
        Assert.Contains("--id", args);
        Assert.Contains("Microsoft.VisualStudioCode", args);
        Assert.Contains("--exact", args);
        Assert.Contains("--silent", args);
        Assert.Contains("--accept-package-agreements", args);
    }

    [Fact]
    public async Task UninstallAsync_uses_uninstall_verb()
    {
        var fake = new FakeWingetExecutor
        {
            ForcedResponse = new WingetRunResult(0, "", "")
        };
        var svc = new WingetInstallService(fake);

        await svc.UninstallAsync("7zip.7zip", null, CancellationToken.None);

        Assert.Equal("uninstall", fake.Calls[0][0]);
        Assert.Contains("7zip.7zip", fake.Calls[0]);
        Assert.DoesNotContain("--accept-package-agreements", fake.Calls[0]);
    }

    [Fact]
    public async Task UpgradeAsync_uses_upgrade_verb()
    {
        var fake = new FakeWingetExecutor
        {
            ForcedResponse = new WingetRunResult(0, "", "")
        };
        var svc = new WingetInstallService(fake);

        await svc.UpgradeAsync("Microsoft.VisualStudioCode", null, CancellationToken.None);

        Assert.Equal("upgrade", fake.Calls[0][0]);
    }

    [Fact]
    public async Task Empty_package_id_throws()
    {
        var svc = new WingetInstallService(new FakeWingetExecutor());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.InstallAsync("", null, CancellationToken.None));
    }

    [Fact]
    public async Task Streams_lines_via_onLine_callback()
    {
        var fake = new FakeWingetExecutor
        {
            ForcedResponse = new WingetRunResult(0, "linha 1\nlinha 2\nlinha 3", "")
        };
        var svc = new WingetInstallService(fake);

        var received = new List<string>();
        await svc.InstallAsync("Foo.Bar", line => received.Add(line), CancellationToken.None);

        Assert.Equal(3, received.Count);
        Assert.Equal("linha 1", received[0]);
        Assert.Equal("linha 3", received[2]);
    }
}
