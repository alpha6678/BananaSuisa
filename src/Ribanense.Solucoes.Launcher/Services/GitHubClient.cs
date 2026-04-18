using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Ribanense.Solucoes.Launcher.Services;

public sealed class GitHubClient : IGitHubClient, IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    public GitHubClient()
        : this(new HttpClient(), ownsHttp: true)
    {
    }

    public GitHubClient(HttpClient http, bool ownsHttp = false)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _ownsHttp = ownsHttp;

        if (_http.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("RibanenseSolucoes-Launcher/1.0");
        }

        string? token = Configuration.LauncherConfig.GitHubToken;
        if (!string.IsNullOrWhiteSpace(token) && _http.DefaultRequestHeaders.Authorization is null)
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<string> GetStringAsync(string url, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
    }

    public async Task<byte[]> GetBytesAsync(string url, IProgress<double>? progress, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        long? total = response.Content.Headers.ContentLength;
        using var ms = new MemoryStream();
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

        byte[] buffer = new byte[81920];
        long read = 0;
        int n;
        while ((n = await stream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await ms.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
            read += n;
            if (total.HasValue && total.Value > 0)
            {
                progress?.Report((double)read / total.Value);
            }
        }

        return ms.ToArray();
    }

    public void Dispose()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }
}
