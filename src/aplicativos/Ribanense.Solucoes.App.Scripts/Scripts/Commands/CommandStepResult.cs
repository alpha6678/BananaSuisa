namespace Ribanense.Solucoes.App.Scripts.Scripts.Commands;

public sealed record CommandStepResult(ShellCommandStep Step, bool Started, int? ExitCode, string? Error)
{
    public bool Succeeded => Started && ExitCode is 0;
}
