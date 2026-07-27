using Ribanense.Solucoes.App.Scripts.Scripts.Dns;
using Xunit;

namespace Ribanense.Solucoes.App.Scripts.Tests;

public class DnsApplyCommandBuilderTests
{
    [Fact]
    public void Build_targets_powershell_with_elevation_and_interface_alias()
    {
        var step = DnsApplyCommandBuilder.Build("Ethernet", new[] { "1.1.1.1" });

        Assert.Equal("powershell.exe", step.Executable);
        Assert.True(step.RequiresElevation);
        Assert.Contains(step.Arguments, a => a.Contains("Set-DnsClientServerAddress"));
        Assert.Contains(step.Arguments, a => a.Contains("Ethernet"));
        Assert.Contains(step.Arguments, a => a.Contains("1.1.1.1"));
    }

    [Fact]
    public void Build_throws_when_interface_alias_is_missing()
    {
        Assert.Throws<ArgumentException>(() => DnsApplyCommandBuilder.Build("", new[] { "1.1.1.1" }));
    }

    [Fact]
    public void Build_throws_when_no_dns_servers_are_provided()
    {
        Assert.Throws<ArgumentException>(() => DnsApplyCommandBuilder.Build("Ethernet", Array.Empty<string>()));
    }
}
