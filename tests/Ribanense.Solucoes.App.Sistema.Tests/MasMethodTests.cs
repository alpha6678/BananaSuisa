using Ribanense.Solucoes.App.Sistema.Services;
using Xunit;

namespace Ribanense.Solucoes.App.Sistema.Tests;

public class MasMethodTests
{
    [Fact]
    public void All_contains_expected_methods_in_order()
    {
        var ids = MasMethod.All.Select(m => m.Id).ToArray();
        Assert.Equal(new[] { "hwid", "ohook", "tsforge", "kms_online", "troubleshoot" }, ids);
    }

    [Theory]
    [InlineData("hwid", "/HWID")]
    [InlineData("ohook", "/Ohook")]
    [InlineData("tsforge", "/Z-WindowsESUOffice")]
    [InlineData("kms_online", "/K-WindowsOffice")]
    public void Methods_map_to_expected_mas_switches(string id, string expectedArg)
    {
        var method = MasMethod.All.Single(m => m.Id == id);
        Assert.Equal(expectedArg, method.Arguments);
    }

    [Fact]
    public void Troubleshoot_has_empty_arguments_to_open_menu()
    {
        Assert.Equal(string.Empty, MasMethod.Troubleshoot.Arguments);
    }
}
