namespace Ribanense.Solucoes.App.Sistema.Services;

public interface IMasRunner
{
    Task<MasRunResult> RunAsync(MasMethod method, IProgress<string>? onLine, CancellationToken ct);
}

public sealed record MasRunResult(bool Success, string? Error, bool Cancelled);
