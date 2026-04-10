using BananaSuisa.Infrastructure.WinGet;

namespace BananaSuisa.Services.Tests;

public sealed class WingetCliTextTableParserTests
{
    [Fact]
    public void TryParsePackageTable_ListPtBr_MapeiaIdEVersaoCorretamente()
    {
        // Cabecalhos reais do winget em PT-BR (UTF-8); evitar literais com acentos quebrados no editor.
        const string header =
            "Nome                                      ID                                        Vers\u00E3o         Dispon\u00EDvel    Origem";
        const string sep =
            "-----------------------------------------------------------------------------------------------------------------------";
        const string row1 =
            "Microsoft Visual C++ v14 Redistributable  Microsoft.VCRedist.2015+.x64              14.50.35719.0                winget";
        const string row2 =
            "Windows Software Development Kit - Windo  Microsoft.WindowsSDK.10.0.26100           10.0.26100.77                winget";
        string sample = string.Join(Environment.NewLine, header, sep, row1, row2);

        List<BananaSuisa.Core.Winget.WingetSearchItem> rows = WingetCliTextTableParser.TryParsePackageTable(sample, 50);

        Assert.Equal(2, rows.Count);
        Assert.Equal("Microsoft.VCRedist.2015+.x64", rows[0].Id);
        Assert.Equal("14.50.35719.0", rows[0].Version);
        Assert.Equal("winget", rows[0].Source);
        Assert.Equal("Microsoft.WindowsSDK.10.0.26100", rows[1].Id);
        Assert.Equal("10.0.26100.77", rows[1].Version);
    }

    [Fact]
    public void TryParsePackageTable_SearchPtBr_LeUltimaColunaOrigem()
    {
        const string header =
            "Nome                          ID                          Vers\u00E3o  Correspond\u00EAncia  Origem";
        const string sep =
            "------------------------------------------------------------------------------------------";
        const string row1 =
            "Mozilla Firefox               9NZVDKPMR9RD                Unknown                  msstore";
        const string row2 =
            "Mozilla Firefox (en-US)       Mozilla.Firefox             149.0.2 Moniker: firefox winget";
        string sample = string.Join(Environment.NewLine, header, sep, row1, row2);

        List<BananaSuisa.Core.Winget.WingetSearchItem> rows = WingetCliTextTableParser.TryParsePackageTable(sample, 50);

        Assert.Equal(2, rows.Count);
        Assert.Equal("9NZVDKPMR9RD", rows[0].Id);
        Assert.Equal("msstore", rows[0].Source);
        Assert.Equal("Mozilla.Firefox", rows[1].Id);
    }
}
