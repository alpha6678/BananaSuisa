using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using Ribanense.Solucoes.Launcher.Configuration;
using Ribanense.Solucoes.Launcher.Domain;
using Ribanense.Solucoes.Launcher.Services;
using Ribanense.Solucoes.PluginSDK.Logging;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.Launcher.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IAppCardHost
{
    private readonly IAppInstallService _installer;
    private readonly IReleaseCheckService _releases;
    private readonly IInstalledAppsRegistry _registry;
    private readonly IAppJsonLog _log;
    private readonly string _aplicativosRoot;

    public MainWindowViewModel(
        ICatalogService catalog,
        IReleaseCheckService releases,
        IInstalledAppsRegistry registry,
        IAppInstallService installer,
        ILauncherUpdateService launcherUpdater,
        IAppJsonLog log,
        string aplicativosRoot)
    {
        _installer = installer;
        _releases = releases;
        _registry = registry;
        _log = log;
        _aplicativosRoot = aplicativosRoot;

        CatalogPage = new CatalogViewModel(catalog, releases, registry, this, log, aplicativosRoot);
        MyAppsPage = new MyAppsViewModel(CatalogPage);
        UpdatesPage = new UpdatesViewModel(CatalogPage, this);
        AboutPage = new AboutViewModel(launcherUpdater);

        Pages = new[] { (PageViewModel)CatalogPage, MyAppsPage, UpdatesPage, AboutPage };
        CurrentPage = CatalogPage;

        NavigateCommand = new AsyncRelayCommand(p => ActivateAsync(p as PageViewModel));
    }

    public CatalogViewModel CatalogPage { get; }
    public MyAppsViewModel MyAppsPage { get; }
    public UpdatesViewModel UpdatesPage { get; }
    public AboutViewModel AboutPage { get; }

    public IReadOnlyList<PageViewModel> Pages { get; }

    private PageViewModel _currentPage = null!;
    public PageViewModel CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public ICommand NavigateCommand { get; }

    public string ProductName => "Ribanense Soluções";

    public async Task ActivateAsync(PageViewModel? page)
    {
        if (page is null || ReferenceEquals(page, CurrentPage)) return;
        CurrentPage = page;
        await page.OnActivatedAsync().ConfigureAwait(true);
    }

    public async Task BootstrapAsync()
    {
        await CatalogPage.OnActivatedAsync().ConfigureAwait(true);
        // Checagem silenciosa da atualizacao do proprio launcher; alimenta o banner e a aba Sobre.
        await AboutPage.CheckForUpdateAsync(silent: true).ConfigureAwait(true);
    }

    // IAppCardHost ------------------------------------------------------

    public async Task InstallAsync(AppCardViewModel card)
    {
        if (card.LatestRelease is null) return;
        card.IsBusy = true;
        card.ErrorMessage = null;
        try
        {
            var progress = new Progress<double>(v => card.Progress = v * 100.0);
            var request = new AppInstallRequest(card.Id, card.LatestRelease, _aplicativosRoot, progress);
            var result = await _installer.InstallAsync(request, CancellationToken.None).ConfigureAwait(true);

            if (!result.Success)
            {
                card.ErrorMessage = result.Error;
                return;
            }

            RefreshCard(card);
        }
        catch (Exception ex)
        {
            _log.Write(AppLogLevel.Error, "install.exception", $"Erro ao instalar {card.Id}.", ex);
            card.ErrorMessage = ex.Message;
        }
        finally
        {
            card.IsBusy = false;
            card.Progress = 0;
        }
    }

    public Task UpdateAsync(AppCardViewModel card) => InstallAsync(card);

    public async Task UninstallAsync(AppCardViewModel card)
    {
        card.IsBusy = true;
        card.ErrorMessage = null;
        try
        {
            var result = _installer.Uninstall(_aplicativosRoot, card.Id);
            if (!result.Success)
            {
                card.ErrorMessage = result.Error;
                return;
            }
            RefreshCard(card);
        }
        catch (Exception ex)
        {
            _log.Write(AppLogLevel.Error, "uninstall.exception", $"Erro ao desinstalar {card.Id}.", ex);
            card.ErrorMessage = ex.Message;
        }
        finally
        {
            card.IsBusy = false;
        }
        await Task.CompletedTask;
    }

    public void Open(AppCardViewModel card)
    {
        if (card.Installed is null) return;
        try
        {
            var psi = new ProcessStartInfo(card.Installed.ExecutablePath)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(card.Installed.ExecutablePath) ?? Environment.CurrentDirectory
            };
            psi.Environment["RIBANENSE_APP_HOME"] = card.Installed.InstallPath;
            psi.Environment["RIBANENSE_APP_DATA"] = Path.Combine(
                LauncherConfig.LauncherDataRoot, "apps", card.Id);
            Directory.CreateDirectory(psi.Environment["RIBANENSE_APP_DATA"]!);

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            _log.Write(AppLogLevel.Error, "open.exception", $"Falha ao abrir {card.Id}.", ex);
            card.ErrorMessage = ex.Message;
        }
    }

    private void RefreshCard(AppCardViewModel card)
    {
        var installed = _registry.Find(_aplicativosRoot, card.Id);
        card.Installed = installed;
        card.Status = _releases.CompareVersions(installed?.Version, card.LatestRelease?.Version);
    }
}
