using System.IO;

namespace Ribanense.Solucoes.App.Winget.Services;

public sealed class WingetLocator : IWingetLocator
{
    public string? TryLocate()
    {
        string? localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            string windowsAppsWinget = Path.Combine(localAppData, "Microsoft", "WindowsApps", "winget.exe");
            if (File.Exists(windowsAppsWinget)) return windowsAppsWinget;
        }

        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path)) return null;

        foreach (string entry in path.Split(
            Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string candidate = Path.Combine(entry, "winget.exe");
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }
}
