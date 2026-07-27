namespace Ribanense.Solucoes.App.Scripts.Scripts.Commands;

/// <summary>
/// Executa uma sequência de comandos de shell automaticamente, um após o
/// outro, reportando progresso em tempo real. Usado como base para scripts
/// futuros baseados em linha de comando.
/// </summary>
public interface ICommandSequenceRunner
{
    Task<IReadOnlyList<CommandStepResult>> RunSequenceAsync(
        IReadOnlyList<ShellCommandStep> steps,
        IProgress<string>? onLine,
        CancellationToken ct,
        bool stopOnError = true);
}
