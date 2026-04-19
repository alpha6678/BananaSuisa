namespace Ribanense.Solucoes.App.Winget.Services.Diagnostics;

public sealed record ElevatedResult(
    int ExitCode,
    string Output,
    bool Cancelled)
{
    public bool Success => ExitCode == 0 && !Cancelled;
}

/// <summary>
/// Executa um script PowerShell como administrador via UAC. Usado para
/// operacoes que exigem elevacao (reset de sources do winget, registro
/// de pacotes Appx, etc.).
/// </summary>
public interface IElevatedCommandRunner
{
    /// <summary>
    /// Grava o script em arquivo temporario, chama <c>Start-Process -Verb RunAs</c>
    /// e retorna exit code + log capturado. Quando o usuario cancela o UAC,
    /// <see cref="ElevatedResult.Cancelled"/> vale <c>true</c> e o ExitCode
    /// fica em 1223 (ERROR_CANCELLED).
    /// </summary>
    Task<ElevatedResult> RunScriptAsync(
        string powerShellScript,
        IProgress<string>? onLine,
        CancellationToken ct);
}
