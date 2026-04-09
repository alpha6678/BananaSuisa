using BananaSuisa.Core.Catalog;
using BananaSuisa.Core.Workspace;

namespace BananaSuisa.Services.Abstractions;

public interface ICatalogLoader
{
    CatalogLoadResult Load(WorkspacePaths paths);
}
