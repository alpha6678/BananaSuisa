using System.IO;
using System.Net;
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

        // API REST do GitHub: sem Accept/versão, alguns ambientes retornam corpo inesperado ou 404 genérico.
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/vnd.github+json");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");

        string? token = Configuration.LauncherConfig.GitHubToken;
        if (!string.IsNullOrWhiteSpace(token) && _http.DefaultRequestHeaders.Authorization is null)
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public async Task<string> GetStringAsync(string url, CancellationToken ct)
    {
        return await SendWithRetriesAsync(
            url,
            async response => await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false),
            ct).ConfigureAwait(false);
    }

    public async Task<byte[]> GetBytesAsync(string url, IProgress<double>? progress, CancellationToken ct)
    {
        return await SendWithRetriesAsync(
            url,
            async response =>
            {
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
            },
            ct).ConfigureAwait(false);
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.TooManyRequests
            or HttpStatusCode.RequestTimeout
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private async Task<T> SendWithRetriesAsync<T>(
        string url,
        Func<HttpResponseMessage, Task<T>> readBody,
        CancellationToken ct,
        int maxAttempts = 4)
    {
        Exception? last = null;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return await readBody(response).ConfigureAwait(false);
                }

                if (attempt < maxAttempts && IsTransient(response.StatusCode))
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), ct).ConfigureAwait(false);
                    continue;
                }

                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts)
            {
                last = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), ct).ConfigureAwait(false);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested && attempt < maxAttempts)
            {
                last = ex;
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), ct).ConfigureAwait(false);
            }
        }

        throw last ?? new HttpRequestException($"Falha ao obter {url} apos {maxAttempts} tentativas.");
    }

    public void Dispose()
    {
        if (_ownsHttp)
        {
            _http.Dispose();
        }
    }
}
