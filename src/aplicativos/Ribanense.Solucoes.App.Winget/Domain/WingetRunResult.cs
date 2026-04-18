namespace Ribanense.Solucoes.App.Winget.Domain;

public sealed record WingetRunResult(
    int ExitCode,
    string Stdout,
    string Stderr)
{
    public bool Success => ExitCode == 0;
}
