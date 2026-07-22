using Ribanense.Solucoes.App.Sistema.Services;
using Xunit;

namespace Ribanense.Solucoes.App.Sistema.Tests;

public class MasMethodTests
{
    [Fact]
    public void All_contains_expected_methods_in_order()
    {
        var ids = MasMethod.All.Select(m => m.Id).ToArray();
        Assert.Equal(new[] { "hwid", "ohook", "kms38", "kms_online", "troubleshoot" }, ids);
    }

    [Fact]
    public void Methods_have_distinct_menu_codes()
    {
        var codes = MasMethod.All.Select(m => m.MenuCode).ToArray();
        Assert.Equal(codes.Distinct().Count(), codes.Length);
    }
}
