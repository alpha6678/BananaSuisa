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
    public async Task ExecuteSearch_fills_results_from_search_service()
    {
        var fakeSearch = new FakeWingetSearchService();
        fakeSearch.ByQuery["demo"] =
        [
            new WingetPackage("Demo", "Demo.App", "1.0.0", "winget")
        ];

        var vm = new SearchViewModel(
            new AliasAwareSearchEnhancer(fakeSearch, new InMemoryAliasCatalog()),
            new FakePackageHost())
        {
            Query = "demo"
        };

        await ExecuteCommandAsync(vm.SearchCommand);

        Assert.Single(vm.Results);
        Assert.Equal("Demo.App", vm.Results[0].Id);
        Assert.Equal("1 resultado(s).", vm.StatusMessage);
    }

    [Fact]
    public void SearchCommand_disabled_when_query_empty()
    {
        var vm = new SearchViewModel(
            new AliasAwareSearchEnhancer(new FakeWingetSearchService(), new InMemoryAliasCatalog()),
            new FakePackageHost());

        Assert.False(vm.SearchCommand.CanExecute(null));
    }

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
