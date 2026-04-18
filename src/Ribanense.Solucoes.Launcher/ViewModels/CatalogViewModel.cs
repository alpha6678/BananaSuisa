using System.Collections.ObjectModel;
using System.Windows.Input;
using Ribanense.Solucoes.Launcher.Domain;
using Ribanense.Solucoes.Launcher.Services;
using Ribanense.Solucoes.PluginSDK.Logging;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.Launcher.ViewModels;

public sealed class CatalogViewModel : PageViewModel
{
    private readonly ICatalogService _catalog;
    private readonly IReleaseCheckService _releases;
    private readonly IInstalledAppsRegistry _registry;
    private readonly IAppCardHost _host;
    private readonly IAppJsonLog _log;
    private readonly string _aplicativosRoot;

    public CatalogViewModel(
        ICatalogService catalog,
        IReleaseCheckService releases,
        IInstalledAppsRegistry registry,
        IAppCardHost host,
        IAppJsonLog log,
        string aplicativosRoot)
    {
        _catalog = catalog;
        _releases = releases;
        _registry = registry;
        _host = host;
        _log = log;
        _aplicativosRoot = aplicativosRoot;

        RefreshCommand = new AsyncRelayCommand(() => LoadAsync(forceRefresh: true));
    }

    public override string Title => "Catálogo";
    public override string Icon => "🗂";

    public ObservableCollection<AppCardViewModel> Cards { get; } = new();

    public ICommand RefreshCommand { get; }

    public override Task OnActivatedAsync() => LoadAsync(forceRefresh: false);

    public async Task LoadAsync(bool forceRefresh)
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            var catalog = await _catalog.GetCatalogAsync(forceRefresh).ConfigureAwait(true);
            var installed = _registry.Scan(_aplicativosRoot);

            Cards.Clear();

            foreach (var entry in catalog.Apps)
            {
                var card = new AppCardViewModel(entry, _host)
                {
                    Installed = installed.FirstOrDefault(a => a.Id == entry.Id)
                };
                Cards.Add(card);

                _ = LoadLatestReleaseAsync(card);
            }

            StatusMessage = Cards.Count == 0
                ? "Catálogo vazio."
                : $"{Cards.Count} app(s) no catálogo.";
        }
        catch (Exception ex)
        {
            _log.Write(AppLogLevel.Warning, "catalog.load", "Falha ao carregar catálogo.", ex);
            StatusMessage = $"Falha ao carregar catálogo: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadLatestReleaseAsync(AppCardViewModel card)
    {
        try
        {
            var release = await _releases.GetLatestReleaseAsync(
                card.Entry.GithubOwner,
                card.Entry.GithubRepo,
                card.Entry.GithubTagPrefix,
                includePrerelease: false,
                CancellationToken.None).ConfigureAwait(true);

            card.LatestRelease = release;
            card.Status = _releases.CompareVersions(card.Installed?.Version, release?.Version);
        }
        catch (Exception ex)
        {
            _log.Write(AppLogLevel.Warning, "release.check", $"Falha ao checar {card.Id}.", ex);
            card.Status = UpdateStatus.ReleaseNotFound;
        }
    }
}
