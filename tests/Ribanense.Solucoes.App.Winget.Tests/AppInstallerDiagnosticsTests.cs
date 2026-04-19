using Ribanense.Solucoes.App.Winget.Domain;
using Ribanense.Solucoes.App.Winget.Services.Diagnostics;
using Ribanense.Solucoes.App.Winget.Tests.Helpers;
using Xunit;

namespace Ribanense.Solucoes.App.Winget.Tests;

public class AppInstallerDiagnosticsTests
{
    [Fact]
    public async Task Inspect_reports_healthy_when_winget_and_pkgs_present()
    {
        var locator = new FakeWingetLocator { Path = @"C:\winget.exe" };
        var exec = new FakeWingetExecutor { ForcedResponse = new WingetRunResult(0, "v1.9.25200", "") };
        var ps = new FakePowerShellRunner();
        ps.ResponsesByKeyword[AppInstallerDiagnostics.AppInstallerName] =
            new PowerShellResult(0, "{\"Name\":\"Microsoft.DesktopAppInstaller\",\"Version\":\"1.22.10570.0\",\"PackageFullName\":\"X_1.22_x86__8...\"}", "");
        ps.ResponsesByKeyword[AppInstallerDiagnostics.VcLibsName] =
            new PowerShellResult(0, "{\"Name\":\"Microsoft.VCLibs.140.00.UWPDesktop\",\"Version\":\"14.0.33728.0\",\"PackageFullName\":\"Y\"}", "");
        ps.ResponsesByKeyword[AppInstallerDiagnostics.UiXamlName] =
            new PowerShellResult(0, "{\"Name\":\"Microsoft.UI.Xaml.2.8\",\"Version\":\"8.2310.30001.0\",\"PackageFullName\":\"Z\"}", "");

        var diag = new AppInstallerDiagnostics(locator, exec, ps);
        var status = await diag.InspectAsync(CancellationToken.None);

        Assert.True(status.Healthy);
        Assert.True(status.Winget.Found);
        Assert.Equal("v1.9.25200", status.Winget.Version);
        Assert.True(status.AppInstaller.Installed);
        Assert.Equal("1.22.10570.0", status.AppInstaller.Version);
        Assert.True(status.VcLibs.Installed);
        Assert.True(status.UiXaml.Installed);
    }

    [Fact]
    public async Task Inspect_flags_winget_not_found()
    {
        var locator = new FakeWingetLocator { Path = null };
        var exec = new FakeWingetExecutor();
        var ps = new FakePowerShellRunner();

        var diag = new AppInstallerDiagnostics(locator, exec, ps);
        var status = await diag.InspectAsync(CancellationToken.None);

        Assert.False(status.Winget.Found);
        Assert.NotNull(status.Winget.Error);
        Assert.False(status.Healthy);
    }

    [Fact]
    public async Task Inspect_flags_missing_package()
    {
        var locator = new FakeWingetLocator { Path = @"C:\winget.exe" };
        var exec = new FakeWingetExecutor { ForcedResponse = new WingetRunResult(0, "v1.9", "") };
        var ps = new FakePowerShellRunner { Default = new PowerShellResult(0, "{}", "") };

        var diag = new AppInstallerDiagnostics(locator, exec, ps);
        var status = await diag.InspectAsync(CancellationToken.None);

        Assert.False(status.AppInstaller.Installed);
        Assert.False(status.VcLibs.Installed);
        Assert.False(status.UiXaml.Installed);
        Assert.False(status.Healthy);
    }

    [Fact]
    public void ParsePackageJson_empty_object_means_not_installed()
    {
        var status = AppInstallerDiagnostics.ParsePackageJson("{}");
        Assert.False(status.Installed);
    }

    [Fact]
    public void ParsePackageJson_malformed_input_is_not_installed()
    {
        var status = AppInstallerDiagnostics.ParsePackageJson("lixo");
        Assert.False(status.Installed);
    }

    [Fact]
    public void ParsePackageJson_extracts_fields()
    {
        var status = AppInstallerDiagnostics.ParsePackageJson(
            "{\"Name\":\"X\",\"Version\":\"1.0\",\"PackageFullName\":\"X_1_...\"}");

        Assert.True(status.Installed);
        Assert.Equal("1.0", status.Version);
        Assert.Equal("X_1_...", status.FullName);
    }
}
