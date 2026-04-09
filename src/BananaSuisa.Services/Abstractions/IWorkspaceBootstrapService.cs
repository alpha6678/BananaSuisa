using BananaSuisa.Core.Workspace;

namespace BananaSuisa.Services.Abstractions;

public interface IWorkspaceBootstrapService
{
    WorkspaceBootstrapResult EnsureInitialized(WorkspacePaths paths);
}
