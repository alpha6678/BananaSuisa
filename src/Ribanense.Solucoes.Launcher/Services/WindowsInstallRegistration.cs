using Microsoft.Win32;

namespace Ribanense.Solucoes.Launcher.Services;

public sealed class WindowsInstallRegistration : IInstallRegistration
{
    public const string UninstallRoot = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

    public void Register(InstallRegistration registration)
    {
        if (registration is null) throw new ArgumentNullException(nameof(registration));
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath(registration.KeyName))
            ?? throw new InvalidOperationException("Não foi possível gravar o registro de desinstalação.");

        key.SetValue("DisplayName", registration.DisplayName);
        key.SetValue("DisplayVersion", registration.DisplayVersion);
        key.SetValue("Publisher", "Ribanense Soluções");
        key.SetValue("InstallLocation", registration.InstallLocation);
        key.SetValue("UninstallString", registration.UninstallString);
        if (!string.IsNullOrWhiteSpace(registration.DisplayIcon))
            key.SetValue("DisplayIcon", registration.DisplayIcon);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    public void Unregister(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName)) return;
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(KeyPath(keyName), throwOnMissingSubKey: false);
        }
        catch
        {
            // best effort
        }
    }

    public InstallRegistration? Find(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName)) return null;
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(KeyPath(keyName));
        return key is null ? null : Read(keyName, key);
    }

    public IReadOnlyList<InstallRegistration> FindByDisplayName(string displayName)
    {
        var found = new List<InstallRegistration>();
        if (string.IsNullOrWhiteSpace(displayName)) return found;

        using RegistryKey? root = Registry.CurrentUser.OpenSubKey(UninstallRoot);
        if (root is null) return found;

        foreach (string name in root.GetSubKeyNames())
        {
            try
            {
                using RegistryKey? key = root.OpenSubKey(name);
                if (key is null) continue;
                string? current = key.GetValue("DisplayName") as string;
                if (!string.Equals(current, displayName, StringComparison.OrdinalIgnoreCase)) continue;
                found.Add(Read(name, key));
            }
            catch
            {
                // chave ilegível
            }
        }

        return found;
    }

    private static InstallRegistration Read(string keyName, RegistryKey key) =>
        new(
            keyName,
            key.GetValue("DisplayName") as string ?? string.Empty,
            key.GetValue("DisplayVersion") as string ?? string.Empty,
            key.GetValue("InstallLocation") as string ?? string.Empty,
            key.GetValue("UninstallString") as string ?? string.Empty,
            key.GetValue("DisplayIcon") as string);

    private static string KeyPath(string keyName) => UninstallRoot + "\\" + keyName;
}
