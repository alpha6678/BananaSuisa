using System.IO;

namespace Ribanense.Solucoes.App.Chocolatey.Services;

public sealed class ChocolateyLocator : IChocolateyLocator
{
    public string? TryLocate()
    {
        string? chocolateyInstall = Environment.GetEnvironmentVariable("ChocolateyInstall");
        if (!string.IsNullOrWhiteSpace(chocolateyInstall))
        {
            string candidate = Path.Combine(chocolateyInstall, "bin", "choco.exe");
            if (File.Exists(candidate)) return candidate;
        }

        string programData = Environment.GetEnvironmentVariable("ProgramData")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (!string.IsNullOrWhiteSpace(programData))
        {
            string candidate = Path.Combine(programData, "chocolatey", "bin", "choco.exe");
            if (File.Exists(candidate)) return candidate;
        }

        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path)) return null;

        foreach (string entry in path.Split(
            Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string candidate = Path.Combine(entry, "choco.exe");
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }
}
