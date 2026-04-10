using BananaSuisa.Core.Winget;

namespace BananaSuisa.Services.Tests;

public sealed class WingetSearchRelevanceTests
{
    [Fact]
    public void BuildWingetCliQuery_RemovePalavrasDescritivas_DeixaTermoPrincipal()
    {
        Assert.Equal("chrome", WingetSearchRelevance.BuildWingetCliQuery("navegador chrome"));
        Assert.Equal("chrome", WingetSearchRelevance.BuildWingetCliQuery("  navegador   chrome  "));
    }

    [Fact]
    public void BuildWingetCliQuery_MantemVariasPalavrasChave()
    {
        Assert.Equal("visual studio", WingetSearchRelevance.BuildWingetCliQuery("visual studio"));
    }

    [Fact]
    public void RankByRelevance_PrimeiroResultadoMaisProximoDoTexto()
    {
        var items = new[]
        {
            new WingetSearchItem("Foo Bar", "X.Foo", "1", "winget", ""),
            new WingetSearchItem("Google Chrome", "Google.Chrome", "1", "winget", ""),
            new WingetSearchItem("Outro", "Other.App", "1", "winget", ""),
        };

        IReadOnlyList<WingetSearchItem> ranked = WingetSearchRelevance.RankByRelevance(items, "navegador chrome", 10);

        Assert.Equal("Google Chrome", ranked[0].Name);
    }

    [Fact]
    public void ScoreAgainstQuery_ContemFrasePontuaAlto()
    {
        var item = new WingetSearchItem("Microsoft Edge", "Microsoft.Edge", "1", "winget", "");
        int s1 = WingetSearchRelevance.ScoreAgainstQuery("edge browser", item);
        int s2 = WingetSearchRelevance.ScoreAgainstQuery("xyz unrelated", item);
        Assert.True(s1 > s2);
    }
}
