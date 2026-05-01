namespace Ribanense.Solucoes.App.Chocolatey.Domain;

public sealed record ChocolateyRunResult(
    int ExitCode,
    string Stdout,
    string Stderr)
{
    public bool Success => ExitCode == 0;
}
