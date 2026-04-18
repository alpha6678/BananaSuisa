namespace Ribanense.Solucoes.Launcher.Domain;

public sealed record AppInstallRequest(
    string AppId,
    ReleaseInfo Release,
    string AplicativosRoot,
    IProgress<double>? Progress);

public sealed record AppInstallResult(
    bool Success,
    string? Error,
    string? InstallPath);

public sealed record AppUninstallResult(
    bool Success,
    string? Error);
