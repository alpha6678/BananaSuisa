namespace Ribanense.Solucoes.Launcher.Domain;

public enum UpdateStatus
{
    NotInstalled,
    UpToDate,
    UpdateAvailable,
    IncompatibleLauncher,
    CorruptedInstallation,
    ReleaseNotFound
}
