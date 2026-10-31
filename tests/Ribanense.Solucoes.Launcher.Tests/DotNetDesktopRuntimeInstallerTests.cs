using Ribanense.Solucoes.Launcher.Services;
using Xunit;

namespace Ribanense.Solucoes.Launcher.Tests;

public class DotNetDesktopRuntimeInstallerTests
{
    [Fact]
    public void BuildInstallerUrl_usa_o_canal_major_minor_da_microsoft()
    {
        string? url = DotNetDesktopRuntimeInstaller.BuildInstallerUrl("10.0.0");

        Assert.Equal("https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe", url);
    }

    [Fact]
    public void DependencyList_nomeia_runtime_e_desktop_runtime()
    {
        string list = DotNetDesktopRuntimeInstaller.DependencyList("10.0.0");

        Assert.Contains(".NET Runtime 10.0 (x64)", list);
        Assert.Contains(".NET Desktop Runtime 10.0 (x64)", list);
    }

    [Fact]
    public void BuildInstallerUrl_rejeita_versao_invalida()
    {
        Assert.Null(DotNetDesktopRuntimeInstaller.BuildInstallerUrl("dez"));
    }

    [Theory]
    [InlineData(0, RuntimeInstallKind.Success)]
    [InlineData(3010, RuntimeInstallKind.RebootRequired)]
    [InlineData(1641, RuntimeInstallKind.RebootRequired)]
    [InlineData(1602, RuntimeInstallKind.Cancelled)]
    [InlineData(1603, RuntimeInstallKind.Failed)]
    public void InterpretExitCode_classifica_o_retorno_do_instalador(int code, RuntimeInstallKind expected)
    {
        Assert.Equal(expected, DotNetDesktopRuntimeInstaller.InterpretExitCode(code));
    }
}
