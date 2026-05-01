using System.Net;
using System.Net.Http;
using Ribanense.Solucoes.App.Chocolatey.Domain;

namespace Ribanense.Solucoes.App.Chocolatey.Services;

/// <summary>
/// Consulta o feed OData NuGet v2 do Chocolatey Community Repository para ordenar por
/// <see cref="ChocolateyGalleryAtomFeedParser"/> — ver documentação oficial sobre <c>orderby=DownloadCount</c>.
/// </summary>
public sealed class ChocolateyPopularPackagesService : IChocolateyPopularPackagesService
{
    private readonly HttpClient _http;

    /// <summary>Máximo de linhas do feed a pedir antes de deduplicar versões do mesmo pacote.</summary>
    private const int DefaultFeedTop = 200;

    public ChocolateyPopularPackagesService(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    public async Task<IReadOnlyList<ChocolateyGalleryEntry>> GetMostDownloadedDistinctAsync(int take, CancellationToken ct)
    {
        if (take <= 0) return Array.Empty<ChocolateyGalleryEntry>();

        int feedTop = Math.Max(DefaultFeedTop, take * 8);
        string url =
            "https://community.chocolatey.org/api/v2/Packages()" +
            "?$orderby=DownloadCount%20desc" +
            "&$top=" + feedTop.ToString(System.Globalization.CultureInfo.InvariantCulture);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("application/atom+xml, application/xml, text/xml");
        request.Headers.TryAddWithoutValidation("User-Agent", "RibanenseSolucoes-ChocolateyApp/1.0");

        using HttpResponseMessage response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.OK)
        {
            return Array.Empty<ChocolateyGalleryEntry>();
        }

        string xml = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        IReadOnlyList<ChocolateyGalleryEntry> rows = ChocolateyGalleryAtomFeedParser.ParseFeed(xml);
        return ChocolateyGalleryAtomFeedParser.DistinctByPackageIdPreserveOrder(rows, take);
    }
}
