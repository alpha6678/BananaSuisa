using Ribanense.Solucoes.App.Winget.Services.Search;
using Xunit;

namespace Ribanense.Solucoes.App.Winget.Tests;

public class SimilarityTests
{
    [Theory]
    [InlineData("Visual Studio Code", "visual studio code")]
    [InlineData("editor de código", "editor de codigo")]
    [InlineData(" VSCode ", "vscode")]
    [InlineData("vs-code", "vs code")]
    [InlineData("Açaí", "acai")]
    public void Normalize_produces_expected_form(string input, string expected)
    {
        Assert.Equal(expected, Similarity.Normalize(input));
    }

    [Fact]
    public void Normalize_null_or_empty_returns_empty()
    {
        Assert.Equal("", Similarity.Normalize(null));
        Assert.Equal("", Similarity.Normalize(""));
        Assert.Equal("", Similarity.Normalize("   "));
    }

    [Fact]
    public void Jaro_identical_is_1()
    {
        Assert.Equal(1.0, Similarity.Jaro("chrome", "chrome"), 3);
    }

    [Fact]
    public void Jaro_empty_strings_return_1_but_with_single_empty_return_0()
    {
        Assert.Equal(1.0, Similarity.Jaro("", ""), 3);
        Assert.Equal(0.0, Similarity.Jaro("", "chrome"), 3);
        Assert.Equal(0.0, Similarity.Jaro("chrome", ""), 3);
    }

    [Fact]
    public void JaroWinkler_typo_scores_above_threshold()
    {
        // "chorme" (typo de "chrome")
        double score = Similarity.JaroWinkler("chrome", "chorme");
        Assert.True(score >= 0.85, $"Esperava score >= 0.85, obtido {score}");
    }

    [Fact]
    public void JaroWinkler_unrelated_words_score_low()
    {
        double score = Similarity.JaroWinkler("chrome", "photoshop");
        Assert.True(score < 0.7, $"Esperava score < 0.7, obtido {score}");
    }

    [Fact]
    public void JaroWinkler_common_prefix_boosts_score()
    {
        double noPrefix = Similarity.JaroWinkler("abc", "xyz");
        double withPrefix = Similarity.JaroWinkler("abcdef", "abcxyz");
        Assert.True(withPrefix > noPrefix);
    }
}
