using BananaSuisa.Core.Text;

namespace BananaSuisa.Services.Tests;

public class FuzzyTextMatcherTests
{
    [Fact]
    public void Normalize_RemovesAccentsAndLowercasesText()
    {
        string normalized = FuzzyTextMatcher.Normalize("Atualiza\u00E7\u00E3o do Cora\u00E7\u00E3o");

        Assert.Equal("atualizacao do coracao", normalized);
    }

    [Theory]
    [InlineData("caixa", "PDV/Caixa basico")]
    [InlineData("chrome", "Google.Chrome")]
    [InlineData("retaguarda", "Retaguarda supermercado")]
    public void IsFuzzyMatch_ReturnsTrueForExpectedMatches(string query, string text)
    {
        bool isMatch = FuzzyTextMatcher.IsFuzzyMatch(query, text);

        Assert.True(isMatch);
    }
}
