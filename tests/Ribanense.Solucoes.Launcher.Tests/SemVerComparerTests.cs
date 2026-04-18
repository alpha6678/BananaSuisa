using Ribanense.Solucoes.Launcher.Services;
using Xunit;

namespace Ribanense.Solucoes.Launcher.Tests;

public class SemVerComparerTests
{
    [Theory]
    [InlineData("1.0.0", "1.0.1", -1)]
    [InlineData("2.0.0", "1.9.9", 1)]
    [InlineData("1.0.0", "1.0.0", 0)]
    [InlineData("1.0.0-beta.1", "1.0.0", -1)]
    public void Compare_respects_semver(string a, string b, int sign)
    {
        Assert.Equal(sign, Math.Sign(SemVerComparer.Instance.Compare(a, b)));
    }

    [Fact]
    public void Compare_invalid_versions_ranked_last_but_does_not_throw()
    {
        Assert.True(SemVerComparer.Instance.Compare("lixo", "1.0.0") < 0);
        Assert.True(SemVerComparer.Instance.Compare("1.0.0", "lixo") > 0);

        int r = SemVerComparer.Instance.Compare("abc", "def");
        Assert.NotEqual(0, r); // ordenação estável por string
    }

    [Fact]
    public void Compare_nulls_handled()
    {
        Assert.Equal(0, SemVerComparer.Instance.Compare(null, null));
        Assert.True(SemVerComparer.Instance.Compare(null, "1.0.0") < 0);
        Assert.True(SemVerComparer.Instance.Compare("1.0.0", null) > 0);
    }
}
