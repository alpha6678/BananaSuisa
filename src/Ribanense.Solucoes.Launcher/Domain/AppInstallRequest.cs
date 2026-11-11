namespace Ribanense.Solucoes.Launcher.Domain;

public enum AppInstallMode
{
    Portable,
    Registered
}

public sealed record AppInstallRequest(
    string AppId,
    ReleaseInfo Release,
    string AplicativosRoot,
    IProgress<double>? Progress,
    IReadOnlyList<ReleaseInfo>? History = null,
    CatalogEntry? Entry = null,
    AppInstallMode? Mode = null);

public sealed record AppInstallResult(
    bool Success,
    string? Error,
    string? InstallPath);

public sealed record AppUninstallResult(
    bool Success,
    string? Error);
