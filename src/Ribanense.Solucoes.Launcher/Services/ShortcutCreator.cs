using System.IO;
using System.Reflection;

namespace Ribanense.Solucoes.Launcher.Services;

public static class ShortcutCreator
{
    public static void Create(string shortcutPath, string targetPath, string description)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);
        Type type = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell indisponível.");
        object shell = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("Não foi possível criar WScript.Shell.");
        object shortcut = type.InvokeMember(
            "CreateShortcut",
            BindingFlags.InvokeMethod,
            null,
            shell,
            new object[] { shortcutPath })!;

        Type shortcutType = shortcut.GetType();
        shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
        shortcutType.InvokeMember(
            "WorkingDirectory",
            BindingFlags.SetProperty,
            null,
            shortcut,
            new object[] { Path.GetDirectoryName(targetPath) ?? string.Empty });
        shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { description });
        shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
    }

    public static void Delete(string shortcutPath)
    {
        try
        {
            if (File.Exists(shortcutPath)) File.Delete(shortcutPath);
        }
        catch
        {
            // best effort
        }
    }
}
