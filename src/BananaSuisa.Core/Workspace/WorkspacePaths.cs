namespace BananaSuisa.Core.Workspace;

public sealed record WorkspacePaths(string ProjectRoot, string VaultPath)
{
    public static WorkspacePaths FromProjectRoot(string projectRoot)
    {
        var vaultPath = Path.Combine(projectRoot, "BananaSuisa_recursos", "BananaSuisa.dat");
        return new WorkspacePaths(projectRoot, vaultPath);
    }

    public static WorkspacePaths FromBaseDirectory(string baseDirectory)
    {
        var vaultPath = Path.Combine(baseDirectory, "BananaSuisa.dat");
        return new WorkspacePaths(baseDirectory, vaultPath);
    }
}
