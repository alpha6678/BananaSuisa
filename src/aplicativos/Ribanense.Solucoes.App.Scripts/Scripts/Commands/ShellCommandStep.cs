namespace Ribanense.Solucoes.App.Scripts.Scripts.Commands;

/// <summary>
/// Um passo de comando de shell que pode ser executado automaticamente em
/// sequência (<see cref="ICommandSequenceRunner"/>) e, ao mesmo tempo, exibido
/// como texto copiável para o usuário rodar manualmente quando preferir ou
/// quando a automação não for possível (ex.: elevação recusada).
/// </summary>
public sealed record ShellCommandStep(
    string Description,
    string Executable,
    IReadOnlyList<string> Arguments,
    bool RequiresElevation = false)
{
    public string ToCommandText()
    {
        if (Arguments.Count == 0) return Executable;
        string args = string.Join(' ', Arguments.Select(QuoteIfNeeded));
        return $"{Executable} {args}";
    }

    private static string QuoteIfNeeded(string arg) =>
        arg.Length > 0 && arg.Contains(' ') && arg[0] != '"' ? $"\"{arg}\"" : arg;
}
