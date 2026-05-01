using Ribanense.Solucoes.App.Winget.Services;
using Xunit;

namespace Ribanense.Solucoes.App.Winget.Tests;

/// <summary>
/// Regressao: saida real do winget 1.11+ no Windows 11 emite dash line
/// contigua (sem espacos separando colunas) e adiciona coluna
/// "Correspondencia" no search.
/// </summary>
public class WingetTableParserRealOutputTests
{
    private const string RealSearchOutputPt = """
Nome                                     ID                                 Versão         Correspondência       Origem
-----------------------------------------------------------------------------------------------------------------------
Google Chrome                            Google.Chrome                      147.0.7727.102 Moniker: chrome       winget
Dichromate                               Dichromate.Browser                 111.0.5563.65  Command: chrome       winget
""";

    private const string RealSearchOutputEn = """
Name                                     Id                                 Version        Match                 Source
-----------------------------------------------------------------------------------------------------------------------
Google Chrome                            Google.Chrome                      147.0.7727.102 Moniker: chrome       winget
""";

    [Fact]
    public void Parses_real_winget_output_with_contiguous_dashes_pt()
    {
        var table = WingetTableParser.Parse(RealSearchOutputPt);

        Assert.NotNull(table);
        Assert.Equal(5, table!.Headers.Count);
        Assert.Equal("Nome", table.Headers[0]);
        Assert.Equal("ID", table.Headers[1]);
        Assert.Equal("Versão", table.Headers[2]);
        Assert.Equal("Correspondência", table.Headers[3]);
        Assert.Equal("Origem", table.Headers[4]);

        Assert.Equal(2, table.Rows.Count);
        Assert.Equal("Google Chrome", table.Rows[0].Values[0]);
        Assert.Equal("Google.Chrome", table.Rows[0].Values[1]);
        Assert.Equal("147.0.7727.102", table.Rows[0].Values[2]);
        Assert.Equal("winget", table.Rows[0].Values[4]);
    }

    [Fact]
    public void Parses_real_winget_output_with_contiguous_dashes_en()
    {
        var table = WingetTableParser.Parse(RealSearchOutputEn);

        Assert.NotNull(table);
        Assert.Equal(5, table!.Headers.Count);
        Assert.Equal("Match", table.Headers[3]);
        Assert.Single(table.Rows);
    }

    [Fact]
    public void SearchService_extracts_packages_from_real_output()
    {
        var list = WingetSearchService.ParseSearchOutput(RealSearchOutputPt);

        Assert.Equal(2, list.Count);
        var chrome = list[0];
        Assert.Equal("Google Chrome", chrome.Name);
        Assert.Equal("Google.Chrome", chrome.Id);
        Assert.Equal("147.0.7727.102", chrome.Version);
        Assert.Equal("winget", chrome.Source);
    }

    [Fact]
    public void GetColumnRangesFromHeader_detects_five_columns()
    {
        string header = "Nome                                     ID                                 Versão         Correspondência       Origem";
        var ranges = WingetTableParser.GetColumnRangesFromHeader(header);

        Assert.Equal(5, ranges.Count);
        Assert.Equal(0, ranges[0].Start);
    }

    [Fact]
    public void GetColumnRangesFromHeader_treats_multi_word_columns_as_single()
    {
        // "Trust Level" (source list) deve virar 1 coluna, nao 2.
        string header = "Name     Argument                       Type                Trust Level";
        var ranges = WingetTableParser.GetColumnRangesFromHeader(header);

        Assert.Equal(4, ranges.Count);
        var lastHeader = header.Substring(ranges[3].Start);
        Assert.Contains("Trust Level", lastHeader);
    }
}
