using Ribanense.Solucoes.App.Winget.ViewModels;
using Ribanense.Solucoes.App.Winget.Services.Search;
using Ribanense.Solucoes.App.Winget.Tests.Helpers;
using Xunit;

namespace Ribanense.Solucoes.App.Winget.Tests;

public class SearchViewModelTests
{
    [Fact]
    public void Constructor_populates_suggested_packages_from_catalog_order()
    {
        var catalog = new InMemoryAliasCatalog(
            new AppAlias
            {
                Id = "Second.App",
                PublicName = "Second",
                Category = "Utilitarios",
                IsSuggested = true,
                SuggestedOrder = 2
            },
            new AppAlias
            {
                Id = "First.App",
                PublicName = "First",
                Category = "Desenvolvimento",
                IsSuggested = true,
                SuggestedOrder = 1
            },
            new AppAlias
            {
                Id = "Hidden.App",
                PublicName = "Hidden"
            });

        var search = new AliasAwareSearchEnhancer(new FakeWingetSearchService(), catalog);
        var vm = new SearchViewModel(search, catalog, new FakePackageHost());

        Assert.Equal(2, vm.SuggestedPackages.Count);
        Assert.Equal("First.App", vm.SuggestedPackages[0].Id);
        Assert.Equal("Desenvolvimento", vm.SuggestedPackages[0].Status);
        Assert.Equal("winget", vm.SuggestedPackages[0].Source);
        Assert.Equal("Second.App", vm.SuggestedPackages[1].Id);
    }

    private sealed class FakePackageHost : IPackageRowHost
    {
        public Task InstallAsync(PackageRowViewModel row) => Task.CompletedTask;
        public Task UninstallAsync(PackageRowViewModel row) => Task.CompletedTask;
        public Task UpgradeAsync(PackageRowViewModel row) => Task.CompletedTask;
    }
}
