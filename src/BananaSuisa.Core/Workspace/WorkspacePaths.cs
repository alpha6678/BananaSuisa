namespace BananaSuisa.Core.Workspace;

public sealed record WorkspacePaths(string ProjectRoot, string VaultPath)
{
    public static WorkspacePaths FromProjectRoot(string projectRoot)
    {
        return new WorkspacePaths(projectRoot, ResolveVaultPath());
    }

    public static WorkspacePaths FromBaseDirectory(string baseDirectory)
    {
        return new WorkspacePaths(baseDirectory, ResolveVaultPath());
    }

    private static string ResolveVaultPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "BananaSuisa.dat");
    }
}
