namespace BananaSuisa.Core.Workspace;

public sealed record WorkspacePaths(
    string ProjectRoot,
    string DevelopmentRoot,
    string ResourcesRoot,
    string MemoryRoot,
    string LogsRoot)
{
    public static WorkspacePaths FromProjectRoot(string projectRoot)
    {
        var developmentRoot = Path.Combine(projectRoot, "BananaSuisa_desenvolvimento");
        var resourcesRoot = Path.Combine(projectRoot, "BananaSuisa_recursos");
        var memoryRoot = Path.Combine(resourcesRoot, "BananaSuisa_memoria");
        var logsRoot = Path.Combine(memoryRoot, "Registros");

        return new WorkspacePaths(projectRoot, developmentRoot, resourcesRoot, memoryRoot, logsRoot);
    }
}
