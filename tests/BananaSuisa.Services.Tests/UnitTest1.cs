using BananaSuisa.Core.Configuration;
using BananaSuisa.Core.Text;
using BananaSuisa.Services.Search;

namespace BananaSuisa.Services.Tests;

public class ConfigurationSearchServiceTests
{
    private readonly ConfigurationSearchService _service = new();

    [Fact]
    public void Normalize_RemovesAccentsAndLowercasesText()
    {
        string normalized = FuzzyTextMatcher.Normalize("Atualiza\u00E7\u00E3o do Cora\u00E7\u00E3o");

        Assert.Equal("atualizacao do coracao", normalized);
    }

    [Theory]
    [InlineData("caixa", "PDV/Caixa basico")]
    [InlineData("chrome", "Google.Chrome")]
    [InlineData("retaguarda", "Retaguarda supermercado")]
    public void IsFuzzyMatch_ReturnsTrueForExpectedMatches(string query, string text)
    {
        bool isMatch = FuzzyTextMatcher.IsFuzzyMatch(query, text);

        Assert.True(isMatch);
    }

    [Fact]
    public void Search_ReturnsConfiguredAppsForMatchingQuery()
    {
        BananaSuisaConfig configuration = CreateSampleConfiguration();

        IReadOnlyList<BananaSuisa.Core.Search.ConfigurationSearchMatch> matches = _service.Search(configuration, "anydesk");

        Assert.Contains(matches, match => match.Kind == "App" && match.DisplayText == "AnyDesk.AnyDesk");
    }

    [Fact]
    public void Search_ReturnsNoMatchesForUnrelatedQuery()
    {
        BananaSuisaConfig configuration = CreateSampleConfiguration();

        IReadOnlyList<BananaSuisa.Core.Search.ConfigurationSearchMatch> matches = _service.Search(configuration, "impressora");

        Assert.Empty(matches);
    }

    [Fact]
    public void BuildPreview_SummarizesIndexedEntriesFromConfiguration()
    {
        BananaSuisaConfig configuration = CreateSampleConfiguration();

        BananaSuisa.Core.Search.ConfigurationSearchPreview preview = _service.BuildPreview(configuration);

        Assert.Equal(2, preview.ProfileCount);
        Assert.Equal(3, preview.UniqueAppCount);
        Assert.Equal(5, preview.IndexedEntryCount);
        Assert.Equal("Caixa", preview.PreviewQuery);
        Assert.Contains(preview.PreviewMatches, match => match.Kind == "Perfil" && match.DisplayText == "Caixa");
    }

    private static BananaSuisaConfig CreateSampleConfiguration() =>
        new()
        {
            Version = "5.0",
            DefaultProfile = "Caixa",
            Profiles = new Dictionary<string, BananaSuisaProfile>(StringComparer.OrdinalIgnoreCase)
            {
                ["Caixa"] = new BananaSuisaProfile
                {
                    Description = "PDV/Caixa basico",
                    Apps = ["Google.Chrome", "AnyDesk.AnyDesk"]
                },
                ["Retaguarda"] = new BananaSuisaProfile
                {
                    Description = "Retaguarda supermercado",
                    Apps = ["Google.Chrome", "AnyDesk.AnyDesk", "7zip.7zip"]
                }
            },
            Settings = new BananaSuisaSettings()
        };
}
