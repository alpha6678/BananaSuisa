using System.ComponentModel;
using System.Diagnostics;

namespace Ribanense.Solucoes.App.Scripts.Scripts.Commands;

/// <summary>
/// Implementação padrão de <see cref="ICommandSequenceRunner"/>. Comandos sem
/// elevação têm stdout/stderr capturados e transmitidos linha a linha (modo
/// "terminal" embutido). Comandos que exigem elevação abrem uma janela
/// própria via UAC (Windows não permite redirecionar saída de processos
/// elevados a partir de um processo não elevado).
/// </summary>
public sealed class CommandSequenceRunner : ICommandSequenceRunner
{
    private const int UacCancelledExitCode = 1223;

    public async Task<IReadOnlyList<CommandStepResult>> RunSequenceAsync(
        IReadOnlyList<ShellCommandStep> steps,
        IProgress<string>? onLine,
        CancellationToken ct,
        bool stopOnError = true)
    {
        if (steps is null) throw new ArgumentNullException(nameof(steps));

        var results = new List<CommandStepResult>();

        foreach (var step in steps)
        {
            ct.ThrowIfCancellationRequested();

            onLine?.Report($"› {step.Description}");
            onLine?.Report($"  comando: {step.ToCommandText()}");

            var result = await RunStepAsync(step, onLine, ct).ConfigureAwait(false);
            results.Add(result);

            if (!result.Started)
            {
                onLine?.Report($"  falha ao iniciar: {result.Error}");
                if (stopOnError) break;
                continue;
            }

            onLine?.Report(result.ExitCode == 0
                ? "  concluído (código 0)."
                : $"  concluído com código {result.ExitCode}.");

            if (stopOnError && result.ExitCode != 0) break;
        }

        return results;
    }

    private static async Task<CommandStepResult> RunStepAsync(ShellCommandStep step, IProgress<string>? onLine, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(step.Executable)
        {
            UseShellExecute = step.RequiresElevation,
            CreateNoWindow = !step.RequiresElevation,
            RedirectStandardOutput = !step.RequiresElevation,
            RedirectStandardError = !step.RequiresElevation,
            WindowStyle = step.RequiresElevation ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden,
        };

        if (step.RequiresElevation)
        {
            psi.Verb = "runas";
        }

        foreach (var arg in step.Arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        try
        {
            if (!process.Start())
            {
                return new CommandStepResult(step, false, null, "o processo não iniciou.");
            }
        }
        catch (Win32Exception win32) when (win32.NativeErrorCode == UacCancelledExitCode)
        {
            return new CommandStepResult(step, false, null, "elevação cancelada pelo usuário (UAC).");
        }
        catch (Exception ex)
        {
            return new CommandStepResult(step, false, null, ex.Message);
        }

        if (!step.RequiresElevation)
        {
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) onLine?.Report("  " + e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) onLine?.Report("  " + e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw;
        }

        return new CommandStepResult(step, true, process.ExitCode, null);
    }
}
