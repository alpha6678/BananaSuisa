namespace BananaSuisa.Core.Workspace;

public sealed record WorkspaceBootstrapResult(
    WorkspacePaths Paths,
    IReadOnlyList<WorkspaceBootstrapItem> Items,
    int CreatedDirectoryCount,
    int SynchronizedFileCount)
{
    public bool IsReady => Items.All(item => item.IsHealthy);
}
