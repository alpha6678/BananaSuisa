using BananaSuisa.Core.Configuration;
using BananaSuisa.Core.Workspace;

namespace BananaSuisa.Services.Abstractions;

public interface IConfigurationLoader
{
    ConfigurationLoadResult Load(WorkspacePaths paths);
}
