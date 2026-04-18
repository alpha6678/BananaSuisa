namespace Ribanense.Solucoes.Launcher.Services;

/// <summary>
/// Abstracao sobre HttpClient para facilitar testes: GET string e GET bytes com progress.
/// </summary>
public interface IGitHubClient
{
    Task<string> GetStringAsync(string url, CancellationToken ct);
    Task<byte[]> GetBytesAsync(string url, IProgress<double>? progress, CancellationToken ct);
}
