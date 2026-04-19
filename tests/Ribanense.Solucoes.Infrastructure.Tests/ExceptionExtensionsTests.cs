using Ribanense.Solucoes.Infrastructure.Logging;
using Xunit;

namespace Ribanense.Solucoes.Infrastructure.Tests;

public class ExceptionExtensionsTests
{
    [Fact]
    public void ToChainedMessage_single_exception_returns_bracketed_form()
    {
        var ex = new InvalidOperationException("falha");
        Assert.Equal("[InvalidOperationException] falha", ex.ToChainedMessage());
    }

    [Fact]
    public void ToChainedMessage_chains_inner_exceptions()
    {
        var inner = new ArgumentException("baixo nivel");
        var outer = new InvalidOperationException("alto nivel", inner);

        string result = outer.ToChainedMessage();

        Assert.Contains("[InvalidOperationException] alto nivel", result);
        Assert.Contains(" -> ", result);
        Assert.Contains("[ArgumentException] baixo nivel", result);
    }

    [Fact]
    public void ToChainedMessage_respects_max_depth()
    {
        var deep = new Exception("d");
        for (int i = 0; i < 20; i++)
        {
            deep = new Exception($"layer-{i}", deep);
        }

        string result = deep.ToChainedMessage(maxDepth: 3);

        int sepCount = System.Text.RegularExpressions.Regex.Matches(result, " -> ").Count;
        // 3 camadas + marcador "..." -> at most 3 separators
        Assert.InRange(sepCount, 2, 3);
        Assert.EndsWith("...", result);
    }

    [Fact]
    public void ToChainedMessage_null_returns_empty()
    {
        Assert.Equal(string.Empty, ((Exception?)null).ToChainedMessage());
    }
}
