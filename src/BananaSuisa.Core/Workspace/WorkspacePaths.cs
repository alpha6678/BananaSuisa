namespace BananaSuisa.Core.Workspace;

public sealed record WorkspacePaths(
    string ProjectRoot,
    string DevelopmentRoot,
    string ResourcesRoot,
    string MemoryRoot,
    string LogsRoot,
    string DataRoot,
    string ProfilesRoot,
    string ScriptsRoot,
    string TempRoot,
    string DriversRoot,
    string InstallersRoot,
    string WingetCacheRoot,
    string ReadmePath,
    string LogFilePath,
    string PayloadConfigPath,
    string PayloadInstallCatalogPath,
    string PayloadTechCatalogPath,
    string ConfigPath,
    string InstallCatalogPath,
    string TechCatalogPath)
{
    public static WorkspacePaths FromProjectRoot(string projectRoot)
    {
        var developmentRoot = Path.Combine(projectRoot, "BananaSuisa_desenvolvimento");
        var resourcesRoot = Path.Combine(projectRoot, "BananaSuisa_recursos");
        var memoryRoot = Path.Combine(resourcesRoot, "BananaSuisa_memoria");
        var logsRoot = Path.Combine(memoryRoot, "Registros");
        var dataRoot = Path.Combine(memoryRoot, "Dados");
        var profilesRoot = Path.Combine(memoryRoot, "Perfis");
        var scriptsRoot = Path.Combine(memoryRoot, "ScriptsExtras");
        var tempRoot = Path.Combine(memoryRoot, "Temporarios");
        var driversRoot = Path.Combine(memoryRoot, "DriversImpressoras");
        var installersRoot = Path.Combine(memoryRoot, "PacotesBaixados");
        var wingetCacheRoot = Path.Combine(installersRoot, "WinGet");
        var readmePath = Path.Combine(memoryRoot, "LEIA-ME.txt");
        var logFilePath = Path.Combine(logsRoot, "BananaSuisa.json");
        var payloadConfigPath = Path.Combine(resourcesRoot, "BananaSuisa.config.json");
        var payloadInstallCatalogPath = Path.Combine(resourcesRoot, "referencia_winget_instalacao_estavel.json");
        var payloadTechCatalogPath = Path.Combine(resourcesRoot, "referencia_winget_ti_estavel.json");
        var configPath = Path.Combine(dataRoot, "BananaSuisa.config.json");
        var installCatalogPath = Path.Combine(dataRoot, "referencia_winget_instalacao_estavel.json");
        var techCatalogPath = Path.Combine(dataRoot, "referencia_winget_ti_estavel.json");

        return new WorkspacePaths(
            projectRoot,
            developmentRoot,
            resourcesRoot,
            memoryRoot,
            logsRoot,
            dataRoot,
            profilesRoot,
            scriptsRoot,
            tempRoot,
            driversRoot,
            installersRoot,
            wingetCacheRoot,
            readmePath,
            logFilePath,
            payloadConfigPath,
            payloadInstallCatalogPath,
            payloadTechCatalogPath,
            configPath,
            installCatalogPath,
            techCatalogPath);
    }
}
