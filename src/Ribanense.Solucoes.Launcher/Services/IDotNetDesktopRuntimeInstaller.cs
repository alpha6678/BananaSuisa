namespace Ribanense.Solucoes.Launcher.Services;

public enum RuntimeInstallKind
{
    Success,
    RebootRequired,
    Cancelled,
    Failed
}

public readonly record struct RuntimeInstallResult(RuntimeInstallKind Kind, string? Detail);

/// <param name="Message">Fase atual e as dependências que entram nela.</param>
/// <param name="Percent">0 a 100 no download. Nulo enquanto o instalador da Microsoft mostra o próprio progresso.</param>
public readonly record struct RuntimeInstallProgress(string Message, double? Percent);

public interface IDotNetDesktopRuntimeInstaller
{
    /// <summary>
    /// Baixa o instalador oficial do .NET Desktop Runtime (x64) e abre a janela de progresso
    /// da Microsoft, com elevacao.
    /// </summary>
    Task<RuntimeInstallResult> InstallAsync(
        string requiredVersion,
        IProgress<RuntimeInstallProgress>? progress,
        CancellationToken ct);
}
