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

    private readonly PartnerInstallProbe _partners;

    public CatalogViewModel(
        ICatalogService catalog,
        IReleaseCheckService releases,
        IInstalledAppsRegistry registry,
        IAppCardHost host,
        IAppJsonLog log,
        string aplicativosRoot,
        PartnerInstallProbe? partners = null)
    {
        _catalog = catalog;
        _releases = releases;
        _registry = registry;
        _host = host;
        _log = log;
        _aplicativosRoot = aplicativosRoot;
        _partners = partners ?? new PartnerInstallProbe(new WindowsInstallRegistration());

        RefreshCommand = new AsyncRelayCommand(() => LoadAsync(forceRefresh: true));
        UpdateAllCommand = new AsyncRelayCommand(_ => UpdateAllAsync(), _ => PendingUpdates.Count > 0 && !IsBusy);

        Cards.CollectionChanged += (_, args) =>
        {
            if (args.NewItems is not null)
            {
                foreach (AppCardViewModel card in args.NewItems)
                    card.PropertyChanged += OnCardChanged;
            }

            if (args.OldItems is not null)
            {
                foreach (AppCardViewModel card in args.OldItems)
                    card.PropertyChanged -= OnCardChanged;
            }

            RefreshPendingUpdates();
        };
    }

    public override string Title => "Catálogo";
    public override string Icon => "🗂";

    public ObservableCollection<AppCardViewModel> Cards { get; } = new();

    /// <summary>Apps instalados que têm versão mais nova publicada.</summary>
    public ObservableCollection<AppCardViewModel> PendingUpdates { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand UpdateAllCommand { get; }

    public string UpdatesSummary => PendingUpdates.Count switch
    {
        0 => "Todos os apps instalados estão na versão mais recente.",
        1 => "1 atualização disponível.",
        _ => $"{PendingUpdates.Count} atualizações disponíveis."
    };

    public bool HasPendingUpdates => PendingUpdates.Count > 0;

    public override Task OnActivatedAsync() => LoadAsync(forceRefresh: false);

    private void OnCardChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppCardViewModel.Status)
            or nameof(AppCardViewModel.Installed)
            or nameof(AppCardViewModel.LatestRelease))
        {
            RefreshPendingUpdates();
        }
    }

    private void RefreshPendingUpdates()
    {
        var desired = Cards.Where(c => c.Status == UpdateStatus.UpdateAvailable).ToList();

        if (desired.Count != PendingUpdates.Count || !desired.SequenceEqual(PendingUpdates))
        {
            PendingUpdates.Clear();
            foreach (var card in desired) PendingUpdates.Add(card);
        }

        OnPropertyChanged(nameof(UpdatesSummary));
        OnPropertyChanged(nameof(HasPendingUpdates));
        (UpdateAllCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    private async Task UpdateAllAsync()
    {
        IsBusy = true;
        try
        {
            foreach (var card in PendingUpdates.ToList())
            {
                await _host.UpdateAsync(card);
            }
        }
        finally
        {
            IsBusy = false;
            RefreshPendingUpdates();
        }
    }

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
                var local = installed.FirstOrDefault(a => a.Id == entry.Id);
                var card = new AppCardViewModel(entry, _host)
                {
                    Installed = local ?? (entry.IsPartner ? _partners.Probe(entry, _aplicativosRoot) : null)
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
            var all = await _releases.GetAllReleasesAsync(
                card.Entry.GithubOwner,
                card.Entry.GithubRepo,
                card.Entry.GithubTagPrefix,
                CancellationToken.None).ConfigureAwait(true);

            card.Releases = all;
            var release = all
                .Where(r => !r.IsPrerelease)
                .OrderByDescending(r => r.Version, SemVerComparer.Instance)
                .FirstOrDefault();

            card.LatestRelease = release;
            if (release is null)
            {
                card.Status = UpdateStatus.ReleaseNotFound;
            }
            else
            {
                card.Status = _releases.CompareVersions(card.Installed?.Version, release.Version);
            }
        }
        catch (Exception ex)
        {
            _log.Write(AppLogLevel.Warning, "release.check", $"Falha ao checar {card.Id}.", ex);
            card.Status = UpdateStatus.ReleaseNotFound;
        }
    }
}
