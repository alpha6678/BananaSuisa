namespace Ribanense.Solucoes.PluginSDK;

/// <summary>
/// Caminhos convencionados de um app. Respeita as variáveis de ambiente
/// injetadas pelo Launcher (RIBANENSE_APP_HOME, RIBANENSE_APP_DATA) e cai
/// para defaults locais quando ausentes, permitindo que o app rode direto
/// sem o Launcher.
/// </summary>
public sealed record AppPaths(
    string AppId,
    string AppHome,
    string AppData,
    string VaultPath)
{
    /// <summary>
    /// Nome do produto usado em paths do sistema (com acentos; o Windows suporta).
    /// </summary>
    public const string ProductFolderName = "Ribanense Soluções";

    /// <summary>
    /// Resolve os caminhos para o app identificado por <paramref name="appId"/>.
    /// </summary>
    /// <param name="appId">ID global do app (ex.: "com.ribanense.winget").</param>
    /// <param name="vaultFileName">
    /// Nome opcional do arquivo de vault (LiteDB .dat). Default: slug derivado do id + ".dat".
    /// </param>
    public static AppPaths Resolve(string appId, string? vaultFileName = null)
    {
        if (string.IsNullOrWhiteSpace(appId))
            throw new ArgumentException("appId obrigatório.", nameof(appId));

        string home = Environment.GetEnvironmentVariable("RIBANENSE_APP_HOME")
            ?? AppContext.BaseDirectory;

        string data = Environment.GetEnvironmentVariable("RIBANENSE_APP_DATA")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                ProductFolderName,
                "apps",
                appId);

        Directory.CreateDirectory(data);

        string vault = Path.Combine(data, vaultFileName ?? $"{DeriveSlug(appId)}.dat");
        return new AppPaths(appId, Path.GetFullPath(home), Path.GetFullPath(data), Path.GetFullPath(vault));
    }

    private static string DeriveSlug(string appId)
    {
        int lastDot = appId.LastIndexOf('.');
        string slug = lastDot >= 0 ? appId[(lastDot + 1)..] : appId;
        return string.IsNullOrWhiteSpace(slug) ? "app" : slug;
    }
}
