using System.IO;
using Ribanense.Solucoes.Launcher.Configuration;
using Ribanense.Solucoes.Launcher.Domain;
using Ribanense.Solucoes.PluginSDK.Payload;

namespace Ribanense.Solucoes.Launcher.Services;

/// <summary>
/// Atalho no Menu Iniciar e entrada em Aplicativos e recursos (HKCU).
/// O modo portátil só grava <c>install-mode.txt</c> e remove o registro.
/// </summary>
public sealed class InstallRegistrationService
{
    public const string ModePortable = "portable";
    public const string ModeRegistered = "registered";

    private readonly IInstallRegistration _store;

    public InstallRegistrationService(IInstallRegistration store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public static string KeyForApp(string appId) => DeploymentLayout.LauncherUninstallKey + " " + appId;

    public static string ShortcutPath(string displayName) =>
        Path.Combine(DeploymentLayout.StartMenuDirectory, SanitizeFileName(displayName) + ".lnk");

    public static AppInstallMode ReadMode(string installDir)
    {
        string path = Path.Combine(installDir, PayloadManifest.InstallModeFileName);
        if (!File.Exists(path)) return AppInstallMode.Portable;
        string text = File.ReadAllText(path).Trim();
        return text.Equals(ModeRegistered, StringComparison.OrdinalIgnoreCase)
            ? AppInstallMode.Registered
            : AppInstallMode.Portable;
    }

    public void ApplyApp(
        AppInstallMode mode,
        string appId,
        string displayName,
        string version,
        string installDir,
        string exePath,
        string launcherExe)
    {
        WriteMode(installDir, mode);
        string shortcut = ShortcutPath(displayName);
        string key = KeyForApp(appId);

        if (mode == AppInstallMode.Portable)
        {
            ShortcutCreator.Delete(shortcut);
            _store.Unregister(key);
            return;
        }

        ShortcutCreator.Create(shortcut, exePath, displayName);
        _store.Register(new InstallRegistration(
            key,
            displayName,
            version,
            installDir,
            $"\"{launcherExe}\" --uninstall-app {appId}",
            exePath));
    }

    public void RemoveApp(string appId, string displayName)
    {
        ShortcutCreator.Delete(ShortcutPath(displayName));
        _store.Unregister(KeyForApp(appId));
    }

    public void RegisterLauncher(string installDir, string exePath, string version)
    {
        string shortcut = ShortcutPath(DeploymentLayout.LauncherUninstallKey);
        ShortcutCreator.Create(shortcut, exePath, DeploymentLayout.LauncherUninstallKey);
        _store.Register(new InstallRegistration(
            DeploymentLayout.LauncherUninstallKey,
            DeploymentLayout.LauncherUninstallKey,
            version,
            installDir,
            $"\"{exePath}\" --uninstall",
            exePath));
    }

    public void UnregisterLauncher()
    {
        ShortcutCreator.Delete(ShortcutPath(DeploymentLayout.LauncherUninstallKey));
        _store.Unregister(DeploymentLayout.LauncherUninstallKey);
    }

    public static void WriteMode(string installDir, AppInstallMode mode)
    {
        Directory.CreateDirectory(installDir);
        File.WriteAllText(
            Path.Combine(installDir, PayloadManifest.InstallModeFileName),
            mode == AppInstallMode.Registered ? ModeRegistered : ModePortable);
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');
        return string.IsNullOrWhiteSpace(name) ? "App" : name.Trim();
    }
}
