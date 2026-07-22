namespace Ribanense.Solucoes.App.Sistema.Services;

public interface IElevatedCommandRunner
{
    Task<ElevatedResult> RunScriptAsync(string powerShellScript, IProgress<string>? onLine, CancellationToken ct);
}

public sealed record ElevatedResult(int ExitCode, string Output, bool Cancelled);
