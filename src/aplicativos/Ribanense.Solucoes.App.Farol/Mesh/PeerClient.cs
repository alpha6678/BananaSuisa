using System.Net.Http;
using Ribanense.Solucoes.App.Farol.Domain;

namespace Ribanense.Solucoes.App.Farol.Mesh;

/// <summary>
/// Cliente da API entre pares. Nunca lança por par indisponível: um farol que
/// não responde é informação, não exceção.
/// </summary>
public sealed class PeerClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly PairingStore _pairing;

    public PeerClient(PairingStore pairing, HttpMessageHandler? handler = null)
    {
        _pairing = pairing ?? throw new ArgumentNullException(nameof(pairing));
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(8);
    }

    public Task<HealthSignal?> GetHealthAsync(PeerBeacon peer, CancellationToken ct) =>
        GetAsync<HealthSignal>(peer, "/health", ct);

    public Task<EvidenceBundle?> GetLatestBundleAsync(PeerBeacon peer, CancellationToken ct) =>
        GetAsync<EvidenceBundle>(peer, "/bundle/latest", ct);

    public Task<EvidenceBundle?> GetBundleAsync(PeerBeacon peer, Guid id, CancellationToken ct) =>
        GetAsync<EvidenceBundle>(peer, $"/bundle/{id:D}", ct);

    private async Task<T?> GetAsync<T>(PeerBeacon peer, string path, CancellationToken ct)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(peer);

        string? hash = _pairing.StoreCodeHash;
        if (hash is null) return null;

        var uri = new Uri($"http://{peer.Address}:{peer.PeerPort}{path}");

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation(PeerHttpServer.StoreHeader, hash);

        try
        {
            using HttpResponseMessage response = await _http
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, ct)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode) return null;

            string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(body) ? null : FarolJson.Deserialize<T>(body);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    public void Dispose() => _http.Dispose();
}
