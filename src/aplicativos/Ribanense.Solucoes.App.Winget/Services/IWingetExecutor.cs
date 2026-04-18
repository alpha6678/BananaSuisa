using Ribanense.Solucoes.App.Winget.Domain;

namespace Ribanense.Solucoes.App.Winget.Services;

public interface IWingetExecutor
{
    /// <summary>
    /// Executa <c>winget.exe</c> com os argumentos dados, reportando linhas
    /// de stdout e stderr via callbacks (para log em tempo real) e
    /// devolvendo o resultado agregado ao final.
    /// </summary>
    Task<WingetRunResult> RunAsync(
        IEnumerable<string> args,
        Action<string>? onStdout = null,
        Action<string>? onStderr = null,
        CancellationToken ct = default);
}
