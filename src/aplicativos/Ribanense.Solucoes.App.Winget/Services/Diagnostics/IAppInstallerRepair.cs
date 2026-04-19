namespace Ribanense.Solucoes.App.Winget.Services.Diagnostics;

public sealed record RepairResult(bool Success, int ExitCode, string Output, bool Cancelled);

public interface IAppInstallerRepair
{
    /// <summary>
    /// Re-registra os pacotes AppX existentes via <c>Add-AppxPackage -Register AppxManifest.xml</c>,
    /// sem baixar nada. Util quando o registro esta corrompido mas os pacotes existem.
    /// Requer UAC.
    /// </summary>
    Task<RepairResult> ReregisterAsync(IProgress<string>? onLine, CancellationToken ct);

    /// <summary>
    /// Baixa o .msixbundle mais recente do GitHub winget-cli releases e instala
    /// via <c>Add-AppxPackage</c>, junto com dependencias. Requer UAC e rede.
    /// </summary>
    Task<RepairResult> DownloadAndInstallLatestAsync(IProgress<string>? onLine, CancellationToken ct);
}
