using System.Diagnostics;

namespace Ribanense.Solucoes.App.Winget.Services.Diagnostics;

/// <summary>
/// Abstracao fina sobre <see cref="Process"/> para permitir substituir por
/// fakes em testes unitarios (nao queremos disparar UAC real).
/// </summary>
public interface IProcessLauncher
{
    /// <summary>
    /// Dispara um processo e espera pelo termino. Retorna o exit code.
    /// </summary>
    Task<int> StartAndWaitAsync(ProcessStartInfo psi, CancellationToken ct);
}

public sealed class ProcessLauncher : IProcessLauncher
{
    public async Task<int> StartAndWaitAsync(ProcessStartInfo psi, CancellationToken ct)
    {
        using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        if (!p.Start())
        {
            throw new InvalidOperationException("Falha ao iniciar o processo.");
        }

        try
        {
            await p.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { }
            throw;
        }

        return p.ExitCode;
    }
}
