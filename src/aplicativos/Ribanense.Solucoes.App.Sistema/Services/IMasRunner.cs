namespace Ribanense.Solucoes.App.Sistema.Services;

public enum MasEngine
{
    Cmd,
    PowerShell
}

public sealed record MasRunOptions(
    bool InteractiveTerminal = true,
    bool ForceRedownload = false,
    MasEngine Engine = MasEngine.Cmd
);

public sealed record MasScriptInfo(bool Exists, DateTime? LastDownloaded, string FilePath);

public interface IMasRunner
{
    Task<MasRunResult> RunAsync(MasMethod method, MasRunOptions? options, IProgress<string>? onLine, CancellationToken ct);
    Task<MasRunResult> RunAsync(MasMethod method, IProgress<string>? onLine, CancellationToken ct);
    Task<bool> RedownloadScriptAsync(IProgress<string>? onLine, CancellationToken ct);
    MasScriptInfo GetScriptInfo();
}

public sealed record MasRunResult(bool Success, string? Error, bool Cancelled);

