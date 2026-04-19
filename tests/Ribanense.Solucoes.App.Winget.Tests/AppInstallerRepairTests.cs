using Ribanense.Solucoes.App.Winget.Services.Diagnostics;
using Ribanense.Solucoes.App.Winget.Tests.Helpers;
using Xunit;

namespace Ribanense.Solucoes.App.Winget.Tests;

public class AppInstallerRepairTests
{
    [Fact]
    public async Task ReregisterAsync_delegates_a_reregister_script_to_elevated_runner()
    {
        var elev = new FakeElevatedCommandRunner
        {
            ForcedResult = new ElevatedResult(0, "[OK]", Cancelled: false)
        };
        var repair = new AppInstallerRepair(elev);

        var result = await repair.ReregisterAsync(null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(elev.Scripts);
        string script = elev.Scripts[0];
        Assert.Contains("Add-AppxPackage -DisableDevelopmentMode -Register", script);
        Assert.Contains(AppInstallerDiagnostics.AppInstallerName, script);
        Assert.Contains(AppInstallerDiagnostics.VcLibsName, script);
        Assert.Contains(AppInstallerDiagnostics.UiXamlName, script);
    }

    [Fact]
    public async Task DownloadAndInstallLatest_builds_script_with_urls()
    {
        var elev = new FakeElevatedCommandRunner();
        var repair = new AppInstallerRepair(elev);

        await repair.DownloadAndInstallLatestAsync(null, CancellationToken.None);

        string script = elev.Scripts[0];
        Assert.Contains(AppInstallerRepair.AppInstallerUrl, script);
        Assert.Contains(AppInstallerRepair.VcLibsUrl, script);
        Assert.Contains("Add-AppxPackage -Path", script);
        Assert.Contains("Invoke-WebRequest", script);
    }

    [Fact]
    public async Task UAC_cancelled_returns_Cancelled_result()
    {
        var elev = new FakeElevatedCommandRunner
        {
            ForcedResult = new ElevatedResult(1223, "", Cancelled: true)
        };
        var repair = new AppInstallerRepair(elev);

        var result = await repair.ReregisterAsync(null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.Cancelled);
        Assert.Equal(1223, result.ExitCode);
    }

    [Fact]
    public void BuildReregisterScript_mentions_all_three_packages()
    {
        string script = AppInstallerRepair.BuildReregisterScript();
        Assert.Contains(AppInstallerDiagnostics.AppInstallerName, script);
        Assert.Contains(AppInstallerDiagnostics.VcLibsName, script);
        Assert.Contains(AppInstallerDiagnostics.UiXamlName, script);
    }

    [Fact]
    public void BuildDownloadInstallScript_mentions_urls_and_commands()
    {
        string script = AppInstallerRepair.BuildDownloadInstallScript();
        Assert.Contains(AppInstallerRepair.AppInstallerUrl, script);
        Assert.Contains(AppInstallerRepair.VcLibsUrl, script);
        Assert.Contains("Invoke-WebRequest", script);
        Assert.Contains("Add-AppxPackage", script);
    }
}
