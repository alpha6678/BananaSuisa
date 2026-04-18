using System.Net.Http;
using Ribanense.Solucoes.Launcher.Services;

namespace Ribanense.Solucoes.Launcher.Tests.Helpers;

public sealed class FakeGitHubClient : IGitHubClient
{
    public Dictionary<string, string> StringResponses { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, byte[]> BytesResponses { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, Exception> Failures { get; } = new(StringComparer.Ordinal);

    public List<string> StringCalls { get; } = new();
    public List<string> BytesCalls { get; } = new();

    public Task<string> GetStringAsync(string url, CancellationToken ct)
    {
        StringCalls.Add(url);
        if (Failures.TryGetValue(url, out var ex)) return Task.FromException<string>(ex);
        if (StringResponses.TryGetValue(url, out var s)) return Task.FromResult(s);
        return Task.FromException<string>(new HttpRequestException($"URL não mockada: {url}"));
    }

    public Task<byte[]> GetBytesAsync(string url, IProgress<double>? progress, CancellationToken ct)
    {
        BytesCalls.Add(url);
        if (Failures.TryGetValue(url, out var ex)) return Task.FromException<byte[]>(ex);
        if (BytesResponses.TryGetValue(url, out var b))
        {
            progress?.Report(1.0);
            return Task.FromResult(b);
        }
        return Task.FromException<byte[]>(new HttpRequestException($"URL não mockada: {url}"));
    }
}
