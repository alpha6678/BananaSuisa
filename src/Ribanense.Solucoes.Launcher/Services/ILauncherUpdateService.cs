using Ribanense.Solucoes.Launcher.Domain;

namespace Ribanense.Solucoes.Launcher.Services;

/// <summary>
/// Verifica e aplica a atualizacao do proprio launcher (executavel single-file).
/// </summary>
public interface ILauncherUpdateService
{
    /// <summary>
    /// Retorna a release mais recente do launcher se houver versao mais nova que a atual;
    /// caso contrario, <c>null</c>.
    /// </summary>
    Task<ReleaseInfo?> CheckForUpdateAsync(CancellationToken ct);

    /// <summary>
    /// Baixa o .exe da release, valida SHA256, substitui o binario atual (rename-and-swap)
    /// e inicia o novo processo com <c>--post-update</c>. Em caso de sucesso, o chamador
    /// deve encerrar a aplicacao para liberar o mutex de instancia unica.
    /// </summary>
    Task<LauncherUpdateResult> DownloadAndApplyAsync(
        ReleaseInfo release, IProgress<double>? progress, CancellationToken ct);
}

/// <summary>
/// Resultado da aplicacao de uma atualizacao do launcher.
/// Quando <see cref="Success"/> e <c>true</c>, o novo processo ja foi iniciado e o
/// processo atual deve encerrar.
/// </summary>
public sealed record LauncherUpdateResult(bool Success, string? Error);
