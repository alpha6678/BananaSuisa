using System.Windows.Input;
using Ribanense.Solucoes.App.Winget.Domain;
using Ribanense.Solucoes.App.Winget.ViewModels;
using Ribanense.Solucoes.App.Winget.Services.Search;
using Ribanense.Solucoes.App.Winget.Tests.Helpers;
using Ribanense.Solucoes.UI.Mvvm;
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
        Assert.Equal("Desenvolvimento", vm.SuggestedPackages[0].Category);
        Assert.Equal("winget", vm.SuggestedPackages[0].Source);
        Assert.Equal("Second.App", vm.SuggestedPackages[1].Id);
        Assert.True(vm.ShowSuggested);
    }

    [Fact]
    public void Typing_query_hides_suggested_before_search()
    {
        var catalog = CreateSuggestedCatalog();
        var vm = new SearchViewModel(
            new AliasAwareSearchEnhancer(new FakeWingetSearchService(), catalog),
            catalog,
            new FakePackageHost());

        Assert.True(vm.ShowSuggested);

        vm.Query = "demo";

        Assert.False(vm.ShowSuggested);
    }

    [Fact]
    public async Task ExecuteSearch_keeps_suggested_hidden_when_results_exist()
    {
        var catalog = CreateSuggestedCatalog();
        var fakeSearch = new FakeWingetSearchService();
        fakeSearch.ByQuery["demo"] =
        [
            new WingetPackage("Demo", "Demo.App", "1.0.0", "winget")
        ];

        var vm = new SearchViewModel(
            new AliasAwareSearchEnhancer(fakeSearch, catalog),
            catalog,
            new FakePackageHost())
        {
            Query = "demo"
        };

        await ExecuteCommandAsync(vm.SearchCommand);

        Assert.False(vm.ShowSuggested);
        Assert.NotEmpty(vm.Results);
    }

    [Fact]
    public async Task Clearing_query_restores_suggested_visibility()
    {
        var catalog = CreateSuggestedCatalog();
        var fakeSearch = new FakeWingetSearchService();
        fakeSearch.ByQuery["demo"] =
        [
            new WingetPackage("Demo", "Demo.App", "1.0.0", "winget")
        ];

        var vm = new SearchViewModel(
            new AliasAwareSearchEnhancer(fakeSearch, catalog),
            catalog,
            new FakePackageHost())
        {
            Query = "demo"
        };

        await ExecuteCommandAsync(vm.SearchCommand);
        Assert.False(vm.ShowSuggested);

        vm.Query = string.Empty;

        Assert.True(vm.ShowSuggested);
        Assert.Empty(vm.Results);
    }

    private static InMemoryAliasCatalog CreateSuggestedCatalog() =>
        new(
            new AppAlias
            {
                Id = "Suggested.App",
                PublicName = "Suggested",
                IsSuggested = true,
                SuggestedOrder = 1
            });

    private static async Task ExecuteCommandAsync(ICommand command)
    {
        var asyncCmd = Assert.IsType<AsyncRelayCommand>(command);
        Assert.True(asyncCmd.CanExecute(null));
        asyncCmd.Execute(null);
        while (asyncCmd.IsExecuting)
        {
            await Task.Delay(10);
        }
    }

    private sealed class FakePackageHost : IPackageRowHost
    {
        public Task InstallAsync(PackageRowViewModel row) => Task.CompletedTask;
        public Task UninstallAsync(PackageRowViewModel row) => Task.CompletedTask;
        public Task UpgradeAsync(PackageRowViewModel row) => Task.CompletedTask;
    }
}
