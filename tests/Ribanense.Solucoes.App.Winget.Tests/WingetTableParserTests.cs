using Ribanense.Solucoes.App.Winget.Services;
using Xunit;

namespace Ribanense.Solucoes.App.Winget.Tests;

public class WingetTableParserTests
{
    private const string SearchOutputEn = """
Name              Id                          Version   Source
----              --                          -------   ------
Visual Studio Code Microsoft.VisualStudioCode  1.95.3    winget
7-Zip             7zip.7zip                    24.09     winget
""";

    private const string SearchOutputPt = """
Nome              Id                          Versão    Origem
----              --                          ------    ------
Visual Studio Code Microsoft.VisualStudioCode  1.95.3    winget
7-Zip             7zip.7zip                    24.09     winget
""";

    private const string ListOutput = """
Name              Id                          Version   Available  Source
----              --                          -------   ---------  ------
Visual Studio Code Microsoft.VisualStudioCode  1.90.0    1.95.3     winget
7-Zip             7zip.7zip                    24.09                winget
""";

    [Fact]
    public void Parse_returns_null_for_empty_input()
    {
        Assert.Null(WingetTableParser.Parse(""));
        Assert.Null(WingetTableParser.Parse("\n\n"));
    }

    [Fact]
    public void Parse_identifies_headers_and_rows()
    {
        var table = WingetTableParser.Parse(SearchOutputEn);

        Assert.NotNull(table);
        Assert.Equal(new[] { "Name", "Id", "Version", "Source" }, table!.Headers);
        Assert.Equal(2, table.Rows.Count);

        Assert.Equal("Visual Studio Code", table.Rows[0].Values[0]);
        Assert.Equal("Microsoft.VisualStudioCode", table.Rows[0].Values[1]);
        Assert.Equal("1.95.3", table.Rows[0].Values[2]);
        Assert.Equal("winget", table.Rows[0].Values[3]);
    }

    [Fact]
    public void Parse_works_with_localized_portuguese_headers()
    {
        var table = WingetTableParser.Parse(SearchOutputPt);

        Assert.NotNull(table);
        Assert.Equal("Nome", table!.Headers[0]);
        Assert.Equal("Versão", table.Headers[2]);
        Assert.Equal("Origem", table.Headers[3]);
    }

    [Fact]
    public void Parse_returns_available_column_for_list_output()
    {
        var table = WingetTableParser.Parse(ListOutput);

        Assert.NotNull(table);
        Assert.Equal(5, table!.Headers.Count);
        Assert.Equal("Available", table.Headers[3]);

        Assert.Equal("1.90.0", table.Rows[0].Values[2]);
        Assert.Equal("1.95.3", table.Rows[0].Values[3]);

        Assert.Equal("24.09", table.Rows[1].Values[2]);
        Assert.Equal(string.Empty, table.Rows[1].Values[3].Trim());
    }
}
