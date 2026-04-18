using Ribanense.Solucoes.PluginSDK.Manifest;
using Xunit;

namespace Ribanense.Solucoes.PluginSDK.Tests;

public class SemVerLooseTests
{
    [Theory]
    [InlineData("1.0.0", true)]
    [InlineData("0.0.1", true)]
    [InlineData("10.20.30", true)]
    [InlineData("1.2.3-beta.1", true)]
    [InlineData("1.2.3-rc.1+build.42", true)]
    [InlineData("1.2", false)]
    [InlineData("v1.0.0", false)]
    [InlineData("1.0.0-", false)]
    [InlineData("abc", false)]
    [InlineData("", false)]
    public void IsValid_handles_common_cases(string input, bool expected)
    {
        Assert.Equal(expected, SemVerLoose.IsValid(input));
    }

    [Theory]
    [InlineData("1.0.0", "1.0.1", -1)]
    [InlineData("1.0.1", "1.0.0", 1)]
    [InlineData("1.0.0", "1.0.0", 0)]
    [InlineData("1.2.0", "1.10.0", -1)]
    [InlineData("2.0.0", "1.99.99", 1)]
    [InlineData("1.0.0-beta.1", "1.0.0", -1)]
    [InlineData("1.0.0", "1.0.0-beta.1", 1)]
    [InlineData("1.0.0-beta.1", "1.0.0-beta.2", -1)]
    public void Compare_respects_semver_order(string left, string right, int sign)
    {
        int result = SemVerLoose.Compare(left, right);
        Assert.Equal(sign, Math.Sign(result));
    }

    [Fact]
    public void Compare_throws_on_invalid()
    {
        Assert.Throws<ArgumentException>(() => SemVerLoose.Compare("abc", "1.0.0"));
        Assert.Throws<ArgumentException>(() => SemVerLoose.Compare("1.0.0", "1.0"));
    }
}
