using System.IO;

namespace Ribanense.Solucoes.Launcher.Configuration;

/// <summary>
/// Configuracao do Launcher. Leitura via variaveis de ambiente com defaults sensatos.
/// Todos os caminhos apontam para %LOCALAPPDATA% por default (area gravavel mesmo
/// quando o .exe esta em Program Files).
/// </summary>
public static class LauncherConfig
{
    public const string LauncherAppId = "com.ribanense.launcher";
    public const string LauncherTagPrefix = "launcher-v";
    public const string DefaultCatalogUrl =
        "https://raw.githubusercontent.com/alpha6678/BananaSuisa/main/catalog/catalog.json";

    public const string DefaultPartnersUrl =
        "https://raw.githubusercontent.com/alpha6678/BananaSuisa/main/catalog/parceiros.json";

    public const string DefaultLauncherGithubOwner = "alpha6678";
    public const string DefaultLauncherGithubRepo = "BananaSuisa";

    /// <summary>Owner do repositorio GitHub que publica as releases do launcher (launcher-v*).</summary>
    public static string LauncherGithubOwner =>
        Environment.GetEnvironmentVariable("RIBANENSE_LAUNCHER_OWNER") ?? DefaultLauncherGithubOwner;

    /// <summary>Repositorio GitHub que publica as releases do launcher (launcher-v*).</summary>
    public static string LauncherGithubRepo =>
        Environment.GetEnvironmentVariable("RIBANENSE_LAUNCHER_REPO") ?? DefaultLauncherGithubRepo;

    public const string ProductFolderName = "Ribanense Soluções";

    public static string CatalogUrl =>
        Environment.GetEnvironmentVariable("RIBANENSE_CATALOG_URL") ?? DefaultCatalogUrl;

    public static DeploymentContext Deployment { get; } = DeploymentContext.Resolve(
        Environment.ProcessPath,
        Environment.GetEnvironmentVariable("RIBANENSE_LAUNCHER_DATA"),
        Environment.GetEnvironmentVariable("RIBANENSE_APLICATIVOS_ROOT"));

    public static string LauncherDataRoot => Deployment.DataRoot;

    public static string LauncherVaultPath =>
        Path.Combine(LauncherDataRoot, "Launcher.dat");

    public static string AplicativosRoot => Deployment.AplicativosRoot;

    public static string? GitHubToken =>
        Environment.GetEnvironmentVariable("GH_TOKEN")
        ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
}
