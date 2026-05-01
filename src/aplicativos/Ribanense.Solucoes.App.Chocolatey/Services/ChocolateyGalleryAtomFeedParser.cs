using System.Globalization;
using System.Xml.Linq;
using Ribanense.Solucoes.App.Chocolatey.Domain;

namespace Ribanense.Solucoes.App.Chocolatey.Services;

internal static class ChocolateyGalleryAtomFeedParser
{
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace Ds = "http://schemas.microsoft.com/ado/2007/08/dataservices";
    private static readonly XNamespace Meta = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";

    /// <summary>
    /// Extrai entradas na ordem do feed. O mesmo pacote pode aparecer em várias versões;
    /// deduplicação fica a cargo do chamador.
    /// </summary>
    internal static IReadOnlyList<ChocolateyGalleryEntry> ParseFeed(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) return Array.Empty<ChocolateyGalleryEntry>();

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        }
        catch (System.Xml.XmlException)
        {
            return Array.Empty<ChocolateyGalleryEntry>();
        }

        var list = new List<ChocolateyGalleryEntry>();

        foreach (XElement entry in doc.Descendants(Atom + "entry"))
        {
            string? id = entry.Element(Atom + "title")?.Value?.Trim();
            if (string.IsNullOrEmpty(id)) continue;

            XElement? props = entry.Element(Meta + "properties")
                ?? entry.Descendants().FirstOrDefault(e => e.Name.LocalName == "properties");

            if (props is null) continue;

            string? version = props.Element(Ds + "Version")?.Value?.Trim();
            if (string.IsNullOrEmpty(version)) continue;

            string? dcText = props.Element(Ds + "DownloadCount")?.Value?.Trim();
            if (!long.TryParse(dcText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long dc))
            {
                dc = 0;
            }

            list.Add(new ChocolateyGalleryEntry(id, version, dc));
        }

        return list;
    }

    internal static IReadOnlyList<ChocolateyGalleryEntry> DistinctByPackageIdPreserveOrder(
        IReadOnlyList<ChocolateyGalleryEntry> ordered,
        int take)
    {
        if (take <= 0 || ordered.Count == 0) return Array.Empty<ChocolateyGalleryEntry>();

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ChocolateyGalleryEntry>(Math.Min(take, ordered.Count));

        foreach (ChocolateyGalleryEntry e in ordered)
        {
            if (!seen.Add(e.Id)) continue;

            result.Add(e);
            if (result.Count >= take) break;
        }

        return result;
    }
}
