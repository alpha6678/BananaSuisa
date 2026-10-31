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
    private readonly IDotNetDesktopRuntimeChecker _runtimeChecker;
    private readonly IDotNetDesktopRuntimeInstaller _runtimeInstaller;
    private readonly string _aplicativosRoot;

    public MainWindowViewModel(
        ICatalogService catalog,
        IReleaseCheckService releases,
        IInstalledAppsRegistry registry,
        IAppInstallService installer,
        ILauncherUpdateService launcherUpdater,
        IAppJsonLog log,
        string aplicativosRoot,
        IDotNetDesktopRuntimeChecker? runtimeChecker = null,
        IDotNetDesktopRuntimeInstaller? runtimeInstaller = null)
    {
        _installer = installer;
        _releases = releases;
        _registry = registry;
        _log = log;
        _runtimeChecker = runtimeChecker ?? new DotNetDesktopRuntimeChecker();
        _runtimeInstaller = runtimeInstaller ?? new DotNetDesktopRuntimeInstaller();
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
        card.RequiredRuntimeVersion = null;
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
        card.RequiredRuntimeVersion = null;
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

        card.ErrorMessage = null;
        card.RequiredRuntimeVersion = null;

        var runtimeCheck = _runtimeChecker.Check(card.Installed.ExecutablePath);
        if (!runtimeCheck.IsSatisfied)
        {
            string versionLabel = FormatMajorMinor(runtimeCheck.RequiredVersion);
            card.RequiredRuntimeVersion = runtimeCheck.RequiredVersion;
            card.ErrorMessage =
                $"Faltam dependências do .NET {versionLabel} (x64): Runtime e Desktop Runtime. " +
                "Instalar .NET baixa o pacote oficial e abre a janela de progresso da Microsoft. O Windows pede permissão de administrador.";
            _log.Write(AppLogLevel.Warning, "open.runtime-missing",
                $"{card.Id} requer .NET Desktop Runtime {versionLabel}, ausente na máquina.");
            return;
        }

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

    public async Task InstallRuntimeAsync(AppCardViewModel card)
    {
        if (card.Installed is null || string.IsNullOrWhiteSpace(card.RequiredRuntimeVersion)) return;

        string version = card.RequiredRuntimeVersion;
        card.IsBusy = true;
        card.ErrorMessage = null;
        card.Progress = 0;
        card.ProgressIndeterminate = false;
        try
        {
            var progress = new Progress<RuntimeInstallProgress>(update =>
            {
                card.ActivityMessage = update.Message;
                card.ProgressIndeterminate = update.Percent is null;
                if (update.Percent is double percent)
                {
                    card.Progress = percent;
                }
            });
            var result = await _runtimeInstaller.InstallAsync(version, progress, CancellationToken.None)
                .ConfigureAwait(true);

            var again = _runtimeChecker.Check(card.Installed.ExecutablePath);
            if (again.IsSatisfied)
            {
                card.RequiredRuntimeVersion = null;
                Open(card);
                return;
            }

            card.ErrorMessage = result.Kind switch
            {
                RuntimeInstallKind.Cancelled => "Instalação do .NET cancelada.",
                RuntimeInstallKind.RebootRequired =>
                    "O .NET foi instalado. Reinicie o Windows e abra o app de novo.",
                _ => result.Detail ?? "Não foi possível instalar o .NET Desktop Runtime."
            };
            _log.Write(AppLogLevel.Warning, "runtime.install",
                $"{card.Id}: {card.ErrorMessage}");
        }
        catch (Exception ex)
        {
            _log.Write(AppLogLevel.Error, "runtime.install", $"Falha ao instalar o .NET para {card.Id}.", ex);
            card.ErrorMessage = ex.Message;
        }
        finally
        {
            card.IsBusy = false;
            card.Progress = 0;
            card.ProgressIndeterminate = false;
            card.ActivityMessage = null;
        }
    }

    private static string FormatMajorMinor(string? version) =>
        Version.TryParse(version, out var v) ? $"{v.Major}.{v.Minor}" : version ?? "";

    private void RefreshCard(AppCardViewModel card)
    {
        var installed = _registry.Find(_aplicativosRoot, card.Id);
        card.Installed = installed;
        card.Status = _releases.CompareVersions(installed?.Version, card.LatestRelease?.Version);
    }
}
