namespace Ribanense.Solucoes.Launcher.Services;

/// <summary>
/// Resultado da checagem de disponibilidade do .NET Desktop Runtime exigido por um app instalado.
/// </summary>
/// <param name="IsSatisfied">
/// <c>true</c> quando o runtime exigido esta instalado (ou quando o app nao depende dele, ex.: self-contained).
/// </param>
/// <param name="RequiredVersion">Versao exigida (ex.: "10.0.0"), preenchida apenas quando <see cref="IsSatisfied"/> e' <c>false</c>.</param>
public readonly record struct RuntimeCheckResult(bool IsSatisfied, string? RequiredVersion)
{
    public static readonly RuntimeCheckResult Satisfied = new(true, null);
}

public interface IDotNetDesktopRuntimeChecker
{
    /// <summary>
    /// Verifica se a maquina atual tem o .NET Desktop Runtime (WindowsDesktop.App) exigido
    /// pelo executavel informado, lendo o <c>*.runtimeconfig.json</c> gerado ao lado dele.
    /// </summary>
    RuntimeCheckResult Check(string appExecutablePath);
}
