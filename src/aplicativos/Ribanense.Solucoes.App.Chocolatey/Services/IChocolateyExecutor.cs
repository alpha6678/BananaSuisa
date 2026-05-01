using Ribanense.Solucoes.App.Chocolatey.Domain;

namespace Ribanense.Solucoes.App.Chocolatey.Services;

public interface IChocolateyExecutor
{
    /// <summary>
    /// Executa <c>choco.exe</c> e devolve stdout/stderr agregados, reportando
    /// linhas em tempo real quando callbacks são informados.
    /// </summary>
    Task<ChocolateyRunResult> RunAsync(
        IEnumerable<string> args,
        Action<string>? onStdout = null,
        Action<string>? onStderr = null,
        CancellationToken ct = default);
}
