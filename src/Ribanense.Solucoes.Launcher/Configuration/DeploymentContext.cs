using System.IO;

namespace Ribanense.Solucoes.Launcher.Configuration;

public enum DeploymentMode
{
    Portable,
    Installed,
    Loose
}

/// <summary>Pastas convencionadas do launcher instalado e da cópia portátil.</summary>
public static class DeploymentLayout
{
    public static string ProductFolderName => PluginSDK.AppPaths.ProductFolderName;

    public static string DefaultDataRoot =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ProductFolderName);

    public static string DefaultInstallDirectory
    {
        get
        {
            string? overrideDir = Environment.GetEnvironmentVariable("RIBANENSE_INSTALL_DIR");
            if (!string.IsNullOrWhiteSpace(overrideDir)) return Path.GetFullPath(overrideDir);

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                ProductFolderName);
        }
    }

    public static string StartMenuDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "Windows",
            "Start Menu",
            "Programs",
            ProductFolderName);

    public const string LauncherExecutableName = "Ribanense.Solucoes.Launcher.exe";
    public const string LauncherUninstallKey = "Ribanense Soluções";
}

/// <summary>
/// Onde esta cópia do launcher vive. Variáveis <c>RIBANENSE_*</c> vencem o marcador
/// portátil, que vence o padrão instalado.
/// </summary>
public sealed class DeploymentContext
{
    public DeploymentContext(
        DeploymentMode mode,
        string executablePath,
        string installDirectory,
        string dataRoot,
        string aplicativosRoot)
    {
        Mode = mode;
        ExecutablePath = executablePath;
        InstallDirectory = installDirectory;
        DataRoot = dataRoot;
        AplicativosRoot = aplicativosRoot;
    }

    public DeploymentMode Mode { get; }
    public string ExecutablePath { get; }
    public string InstallDirectory { get; }
    public string DataRoot { get; }
    public string AplicativosRoot { get; }

    public string ModeLabel => Mode switch
    {
        DeploymentMode.Portable => "Portátil",
        DeploymentMode.Installed => "Instalado",
        _ => "Cópia avulsa"
    };

    public static DeploymentContext Resolve(
        string? executablePath = null,
        string? environmentDataRoot = null,
        string? environmentAppsRoot = null)
    {
        string exe = executablePath ?? Environment.ProcessPath ?? AppContext.BaseDirectory;
        string exeDir = ResolveDirectory(exe);
        bool portable = File.Exists(Path.Combine(exeDir, PluginSDK.Payload.PayloadManifest.PortableMarkerFileName));
        bool installed = !portable && PathsEqual(exeDir, DeploymentLayout.DefaultInstallDirectory);

        string data = !string.IsNullOrWhiteSpace(environmentDataRoot)
            ? environmentDataRoot
            : portable
                ? Path.Combine(exeDir, "dados")
                : DeploymentLayout.DefaultDataRoot;

        string apps = !string.IsNullOrWhiteSpace(environmentAppsRoot)
            ? environmentAppsRoot
            : portable && string.IsNullOrWhiteSpace(environmentDataRoot)
                ? Path.Combine(exeDir, "aplicativos")
                : Path.Combine(data, "aplicativos");

        DeploymentMode mode = portable
            ? DeploymentMode.Portable
            : installed ? DeploymentMode.Installed : DeploymentMode.Loose;

        return new DeploymentContext(
            mode,
            File.Exists(exe) ? Path.GetFullPath(exe) : Path.Combine(exeDir, DeploymentLayout.LauncherExecutableName),
            Path.GetFullPath(exeDir),
            Path.GetFullPath(data),
            Path.GetFullPath(apps));
    }

    private static string ResolveDirectory(string path)
    {
        if (Directory.Exists(path) && !File.Exists(path))
            return Path.GetFullPath(path);
        string? dir = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(dir))
            return Path.GetFullPath(AppContext.BaseDirectory);
        return Path.GetFullPath(dir);
    }

    private static bool PathsEqual(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
