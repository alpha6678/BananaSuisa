namespace Ribanense.Solucoes.Launcher.Services;

public sealed record InstallRegistration(
    string KeyName,
    string DisplayName,
    string DisplayVersion,
    string InstallLocation,
    string UninstallString,
    string? DisplayIcon);

public interface IInstallRegistration
{
    void Register(InstallRegistration registration);
    void Unregister(string keyName);
    InstallRegistration? Find(string keyName);
    IReadOnlyList<InstallRegistration> FindByDisplayName(string displayName);
}
