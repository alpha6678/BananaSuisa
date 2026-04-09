using BananaSuisa.Core.Configuration;
using BananaSuisa.Core.Search;

namespace BananaSuisa.Services.Abstractions;

public interface IConfigurationSearchService
{
    IReadOnlyList<ConfigurationSearchEntry> BuildEntries(BananaSuisaConfig configuration);

    IReadOnlyList<ConfigurationSearchMatch> Search(BananaSuisaConfig configuration, string query, int limit = 10);

    ConfigurationSearchPreview BuildPreview(BananaSuisaConfig configuration, int previewLimit = 5);
}
