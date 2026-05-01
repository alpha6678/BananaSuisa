using Ribanense.Solucoes.App.Chocolatey.Domain;
using Ribanense.Solucoes.App.Chocolatey.Services;
using Xunit;

namespace Ribanense.Solucoes.App.Chocolatey.Tests;

public class ChocolateyGalleryAtomFeedParserTests
{
    [Fact]
    public void ParseFeed_reads_title_version_and_download_count()
    {
        string xml = """
            <?xml version="1.0" encoding="utf-8" standalone="yes"?>
            <feed xmlns="http://www.w3.org/2005/Atom"
                  xmlns:d="http://schemas.microsoft.com/ado/2007/08/dataservices"
                  xmlns:m="http://schemas.microsoft.com/ado/2007/08/dataservices/metadata">
              <entry>
                <title type="text">git</title>
                <m:properties>
                  <d:Version>2.44.0</d:Version>
                  <d:DownloadCount m:type="Edm.Int32">2500000</d:DownloadCount>
                </m:properties>
              </entry>
            </feed>
            """;

        var rows = ChocolateyGalleryAtomFeedParser.ParseFeed(xml);

        Assert.Single(rows);
        Assert.Equal("git", rows[0].Id);
        Assert.Equal("2.44.0", rows[0].Version);
        Assert.Equal(2_500_000, rows[0].DownloadCount);
    }

    [Fact]
    public void DistinctByPackageIdPreserveOrder_keeps_first_occurrence()
    {
        var ordered = new ChocolateyGalleryEntry[]
        {
            new("a", "1", 100),
            new("a", "2", 100),
            new("b", "1", 50)
        };

        var distinct = ChocolateyGalleryAtomFeedParser.DistinctByPackageIdPreserveOrder(ordered, take: 10);

        Assert.Equal(2, distinct.Count);
        Assert.Equal("a", distinct[0].Id);
        Assert.Equal("1", distinct[0].Version);
        Assert.Equal("b", distinct[1].Id);
    }
}
