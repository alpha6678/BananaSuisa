using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using BananaSuisa.App.Views;
using BananaSuisa.Core.Catalog;
using BananaSuisa.Core.Diagnostics;
using BananaSuisa.Core.Logging;
using BananaSuisa.Core.Text;
using BananaSuisa.Core.Winget;
using BananaSuisa.Services.Abstractions;

namespace BananaSuisa.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private object? _currentView;
    private string _navigationSelectedKey = "Dashboard";
    private bool _isLoading;
    private string _loadingMessage = "Carregando...";
    private bool _isInstallMode;
    private string _installSubKey = "InstallOverview";
    private object? _installChildView;
    private string _activityLog = string.Empty;
    private string _wingetProbeSummary = "Clique em Verificar para analisar o winget nesta maquina.";
    private string _uwpProbeSummary = "Clique em Verificar para analisar App Installer e Loja (quando existir).";
    private string _wingetSearchQuery = string.Empty;

    private readonly IWingetProvisioningService _wingetProvisioning;
    private readonly IUwpAppInstallerProvisioningService _uwpProvisioning;
    private readonly IWingetSearchService _wingetSearch;
    private readonly IWingetPackageInstallService _wingetPackageInstall;
    private readonly IAppJsonLog _appLog;

    private readonly List<WingetSearchItem> _installTabInstalledItems = [];
    private readonly HashSet<string> _installedIdsForInstallTab = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PendingInstallEntry> _pendingInstall = new(StringComparer.OrdinalIgnoreCase);

    private CancellationTokenSource? _installCts;
    private bool _isInstallingPackages;
    private string _installCatalogModeLabel = "Sugestões validadas";
    private bool _isShowingOfflineList = true;
    private bool _showRetrySummary;

    private readonly List<WingetCatalogPickRowViewModel> _offlineValidatedRows = [];
    private readonly List<WingetCatalogPickRowViewModel> _repositorySearchRows = [];
    private readonly List<InstallBatchResultEntry> _succeededInstalls = [];
    private readonly List<InstallBatchResultEntry> _failedInstalls = [];
    private readonly ObservableCollection<RetryCandidateViewModel> _retryCandidates = [];

    private sealed record PendingInstallEntry(string Name, string Id, string Version, string Source, string InstallationOrigin);

    public object? CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    public string NavigationSelectedKey
    {
        get => _navigationSelectedKey;
        set => SetProperty(ref _navigationSelectedKey, value);
    }

    public bool IsInstallMode
    {
        get => _isInstallMode;
        set => SetProperty(ref _isInstallMode, value);
    }

    public string InstallSubKey
    {
        get => _installSubKey;
        set => SetProperty(ref _installSubKey, value);
    }

    public object? InstallChildView
    {
        get => _installChildView;
        set => SetProperty(ref _installChildView, value);
    }

    public string ActivityLog
    {
        get => _activityLog;
        set
        {
            if (SetProperty(ref _activityLog, value))
                OnPropertyChanged(nameof(ShowActivityLogStrip));
        }
    }

    public bool ShowActivityLogStrip => !string.IsNullOrWhiteSpace(ActivityLog);

    public string WingetProbeSummary
    {
        get => _wingetProbeSummary;
        set => SetProperty(ref _wingetProbeSummary, value);
    }

    public string UwpProbeSummary
    {
        get => _uwpProbeSummary;
        set => SetProperty(ref _uwpProbeSummary, value);
    }

    public string WingetSearchQuery
    {
        get => _wingetSearchQuery;
        set
        {
            if (SetProperty(ref _wingetSearchQuery, value))
                ApplyOfflineFilter(value);
        }
    }

    public ObservableCollection<WingetSearchRowViewModel> WingetSearchRows { get; } = new();
    public ObservableCollection<WingetCatalogPickRowViewModel> WingetCatalogSearchRows { get; } = new();
    public ObservableCollection<WingetCatalogPickRowViewModel> RecommendedApps { get; } = new();

    public string InstallCatalogModeLabel
    {
        get => _installCatalogModeLabel;
        set => SetProperty(ref _installCatalogModeLabel, value);
    }

    public bool IsShowingOfflineList
    {
        get => _isShowingOfflineList;
        set => SetProperty(ref _isShowingOfflineList, value);
    }

    public bool ShowRetrySummary
    {
        get => _showRetrySummary;
        set => SetProperty(ref _showRetrySummary, value);
    }

    public ObservableCollection<RetryCandidateViewModel> RetryCandidates => _retryCandidates;
    public int BatchSucceededCount => _succeededInstalls.Count;
    public int BatchFailedCount => _failedInstalls.Count;

    public bool HasPendingInstalls =>
        _pendingInstall.Keys.Any(id => !_installedIdsForInstallTab.Contains(id));

    public bool IsInstallingPackages
    {
        get => _isInstallingPackages;
        private set
        {
            if (SetProperty(ref _isInstallingPackages, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string LoadingMessage
    {
        get => _loadingMessage;
        set => SetProperty(ref _loadingMessage, value);
    }

    // -- Propriedades de exibicao (Dashboard) --

    public string Title { get; }
    public string Subtitle { get; }
    public string AppVersionLabel { get; }
    public string WingetPath { get; }
    public string GeneratedAt { get; }

    // -- Commands --

    public ICommand NavigateCommand { get; }
    public ICommand ExitInstallModeCommand { get; }
    public ICommand NavigateInstallSubCommand { get; }
    public ICommand ProbeWingetCommand { get; }
    public ICommand InstallWingetBundleCommand { get; }
    public ICommand ReinstallWingetCommand { get; }
    public ICommand ProbeUwpCommand { get; }
    public ICommand InstallUwpBundleCommand { get; }
    public ICommand SearchWingetCatalogCommand { get; }
    public ICommand InstallPendingPackagesCommand { get; }
    public ICommand CancelInstallCommand { get; }
    public ICommand LoadRecommendationsCommand { get; }
    public ICommand CloseRetrySummaryCommand { get; }
    public ICommand RetryAllSuggestedCommand { get; }
    public ICommand RetryOneByOneCommand { get; }

    private MainWindowViewModel(
        string title,
        string subtitle,
        string appVersion,
        string wingetPath,
        string generatedAt,
        IWingetProvisioningService wingetProvisioning,
        IUwpAppInstallerProvisioningService uwpProvisioning,
        IWingetSearchService wingetSearch,
        IWingetPackageInstallService wingetPackageInstall,
        IAppJsonLog appLog)
    {
        _wingetProvisioning = wingetProvisioning;
        _uwpProvisioning = uwpProvisioning;
        _wingetSearch = wingetSearch;
        _wingetPackageInstall = wingetPackageInstall;
        _appLog = appLog;

        Title = title;
        Subtitle = subtitle;
        AppVersionLabel = string.IsNullOrWhiteSpace(appVersion) ? "v?" : $"v{appVersion}";
        WingetPath = wingetPath;
        GeneratedAt = generatedAt;

        NavigateCommand = new RelayCommand(Navigate);
        ExitInstallModeCommand = new RelayCommand(_ => ExitInstallMode());
        NavigateInstallSubCommand = new RelayCommand(NavigateInstallSub);
        ProbeWingetCommand = new AsyncRelayCommand(_ => ProbeWingetAsync());
        InstallWingetBundleCommand = new AsyncRelayCommand(_ => InstallWingetBundleAsync());
        ReinstallWingetCommand = new AsyncRelayCommand(_ => ReinstallWingetAsync());
        ProbeUwpCommand = new AsyncRelayCommand(_ => ProbeUwpAsync());
        InstallUwpBundleCommand = new AsyncRelayCommand(_ => InstallUwpBundleAsync());
        SearchWingetCatalogCommand = new AsyncRelayCommand(_ => SearchWingetCatalogAsync());
        InstallPendingPackagesCommand = new AsyncRelayCommand(
            _ => InstallPendingPackagesAsync(),
            _ => HasPendingInstalls && !IsInstallingPackages);
        CancelInstallCommand = new RelayCommand(_ => _installCts?.Cancel(), _ => IsInstallingPackages);
        LoadRecommendationsCommand = new AsyncRelayCommand(_ => LoadRecommendationsAsync());
        CloseRetrySummaryCommand = new RelayCommand(_ => ShowRetrySummary = false);
        RetryAllSuggestedCommand = new AsyncRelayCommand(_ => RetryAllSuggestedAsync());
        RetryOneByOneCommand = new AsyncRelayCommand(_ => RetryOneByOneAsync());

        CurrentView = new DashboardView { DataContext = this };
    }

    // -- Navegacao --

    private void Navigate(object? parameter)
    {
        if (parameter is not string viewName) return;

        if (viewName == "Install")
        {
            NavigationSelectedKey = viewName;
            IsInstallMode = true;
            InstallSubKey = "InstallOverview";
            InstallChildView = new InstallOverviewView { DataContext = this };
            CurrentView = new InstallShellView { DataContext = this };
            return;
        }

        IsInstallMode = false;
        NavigationSelectedKey = viewName;
        ActivityLog = string.Empty;

        CurrentView = viewName switch
        {
            "Dashboard" => new DashboardView { DataContext = this },
            _ => CurrentView
        };
    }

    private void ExitInstallMode()
    {
        _installCts?.Cancel();
        IsInstallMode = false;
        NavigationSelectedKey = "Dashboard";
        InstallChildView = null;
        InstallSubKey = "InstallOverview";
        _pendingInstall.Clear();
        _installTabInstalledItems.Clear();
        _installedIdsForInstallTab.Clear();
        WingetSearchRows.Clear();
        WingetCatalogSearchRows.Clear();
        CurrentView = new DashboardView { DataContext = this };
    }

    private void NavigateInstallSub(object? parameter)
    {
        if (parameter is not string key) return;

        InstallSubKey = key;
        ActivityLog = string.Empty;
        InstallChildView = key switch
        {
            "InstallOverview" => new InstallOverviewView { DataContext = this },
            "InstallRun" => new InstallRunView { DataContext = this },
            "InstallUninstall" => new InstallUninstallView { DataContext = this },
            "InstallWinget" => new WingetProvisionView { DataContext = this },
            "InstallUwp" => new UwpProvisionView { DataContext = this },
            _ => InstallChildView
        };

        if (key == "InstallRun")
        {
            _ = LoadInstalledWingetPackagesForInstallTabAsync();
            if (string.IsNullOrWhiteSpace(WingetSearchQuery))
                _ = LoadRecommendationsIntoSearchGridAsync();
        }
        else if (key == "InstallUninstall")
        {
            _ = LoadInstalledPackagesForUninstallTabAsync();
        }
    }

    // -- Recomendacoes e catalogo --

    private async Task LoadRecommendationsAsync()
    {
        if (RecommendedApps.Count > 0) return;

        try
        {
            var accessibleApps = ItProfessionalsCatalog.GetRecommendations();

            App.Current.Dispatcher.Invoke(() =>
            {
                RecommendedApps.Clear();
                foreach (var item in accessibleApps)
                {
                    RecommendedApps.Add(new WingetCatalogPickRowViewModel(
                        this, item.Name, item.PackageId, "Latest", "winget", item.Category, false));
                }
            });
        }
        catch (Exception ex)
        {
            ActivityLog = $"Erro ao carregar recomendações curadas: {ex.Message}";
            _appLog.Write(AppLogLevel.Error, "catalog.recommendations", "Erro ao carregar recomendações de TI.", ex);
        }
    }

    private async Task LoadRecommendationsIntoSearchGridAsync()
    {
        await LoadRecommendationsAsync();

        App.Current.Dispatcher.Invoke(() =>
        {
            _offlineValidatedRows.Clear();
            foreach (var app in RecommendedApps)
            {
                _offlineValidatedRows.Add(new WingetCatalogPickRowViewModel(
                    this, app.Name, app.Id, app.Version, app.Source, app.InstallationOrigin,
                    _pendingInstall.ContainsKey(app.Id)));
            }

            IsShowingOfflineList = true;
            InstallCatalogModeLabel = "Sugestões validadas";
            SyncVisibleCatalogRows(_offlineValidatedRows);
        });
    }

    private void ApplyOfflineFilter(string query)
    {
        if (!IsInstallMode || InstallSubKey != "InstallRun") return;
        if (!IsShowingOfflineList && !string.IsNullOrWhiteSpace(query)) return;

        if (string.IsNullOrWhiteSpace(query))
        {
            IsShowingOfflineList = true;
            InstallCatalogModeLabel = "Sugestões validadas";
            SyncVisibleCatalogRows(_offlineValidatedRows);
            return;
        }

        IsShowingOfflineList = true;
        InstallCatalogModeLabel = "Sugestões validadas (filtrado)";

        var filtered = _offlineValidatedRows
            .Where(r => FuzzyTextMatcher.IsFuzzyMatch(query, r.Name) || FuzzyTextMatcher.IsFuzzyMatch(query, r.Id))
            .ToList();

        SyncVisibleCatalogRows(filtered);
    }

    private void SyncVisibleCatalogRows(List<WingetCatalogPickRowViewModel> source)
    {
        WingetCatalogSearchRows.Clear();
        foreach (var row in source)
        {
            row.SetSelectedSilent(_pendingInstall.ContainsKey(row.Id));
            WingetCatalogSearchRows.Add(row);
        }
    }

    // -- Log de atividade --

    private void AppendInstallLog(string line)
    {
        ActivityLog += $"[{DateTime.Now:HH:mm:ss}] {line}\r\n";
        _appLog.Write(AppLogLevel.Information, "install.ui", line);
    }

    // -- Winget provisioning --

    private async Task ProbeWingetAsync()
    {
        IsLoading = true;
        LoadingMessage = "Verificando winget...";
        try { await ProbeWingetCoreAsync().ConfigureAwait(true); }
        catch (Exception ex)
        {
            _appLog.Write(AppLogLevel.Error, "install.winget", "ProbeWingetAsync falhou.", ex);
            AppendInstallLog($"[winget] ERRO interno: {ex.Message}");
        }
        finally { IsLoading = false; }
    }

    private async Task ProbeWingetCoreAsync()
    {
        var r = await _wingetProvisioning.ProbeAsync().ConfigureAwait(true);
        WingetProbeSummary = r.Summary;
        AppendInstallLog($"[winget] {r.Summary}");
        if (r.IsHealthy)
            AppendInstallLog($"[winget] Integridade: OK (source list exit={r.SourceListExitCode}).");
        else
            AppendInstallLog("[winget] Integridade: possivel falha — tente Instalar bundle ou Reinstalar.");
    }

    private async Task InstallWingetBundleAsync()
    {
        IsLoading = true;
        LoadingMessage = "Baixando e instalando App Installer / winget (GitHub oficial)...";
        try
        {
            var r = await _wingetProvisioning.InstallLatestFromGitHubReleaseAsync().ConfigureAwait(true);
            AppendInstallLog(r.Succeeded ? $"[winget] {r.Message}" : $"[winget] ERRO: {r.Message}");
            await ProbeWingetCoreAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _appLog.Write(AppLogLevel.Error, "install.winget", "InstallWingetBundleAsync falhou.", ex);
            AppendInstallLog($"[winget] ERRO interno: {ex.Message}");
        }
        finally { IsLoading = false; }
    }

    private async Task ReinstallWingetAsync()
    {
        IsLoading = true;
        LoadingMessage = "Removendo App Installer e reinstalando do release oficial...";
        try
        {
            var r = await _wingetProvisioning.ReinstallAsync().ConfigureAwait(true);
            AppendInstallLog(r.Succeeded ? $"[winget] {r.Message}" : $"[winget] ERRO: {r.Message}");
            await ProbeWingetCoreAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _appLog.Write(AppLogLevel.Error, "install.winget", "ReinstallWingetAsync falhou.", ex);
            AppendInstallLog($"[winget] ERRO interno: {ex.Message}");
        }
        finally { IsLoading = false; }
    }

    // -- UWP provisioning --

    private async Task ProbeUwpAsync()
    {
        IsLoading = true;
        LoadingMessage = "Verificando pacotes App Installer e Loja...";
        try { await ProbeUwpCoreAsync().ConfigureAwait(true); }
        catch (Exception ex)
        {
            _appLog.Write(AppLogLevel.Error, "install.uwp", "ProbeUwpAsync falhou.", ex);
            AppendInstallLog($"[uwp] ERRO interno: {ex.Message}");
        }
        finally { IsLoading = false; }
    }

    private async Task ProbeUwpCoreAsync()
    {
        var r = await _uwpProvisioning.ProbeAsync().ConfigureAwait(true);
        UwpProbeSummary = r.Summary;
        AppendInstallLog($"[uwp] {r.Summary}");
    }

    private async Task InstallUwpBundleAsync()
    {
        IsLoading = true;
        LoadingMessage = "Instalando pacote oficial (App Installer)...";
        try
        {
            var r = await _uwpProvisioning.InstallOrRepairAppInstallerFromOfficialBundleAsync().ConfigureAwait(true);
            AppendInstallLog(r.Succeeded ? $"[uwp] {r.Message}" : $"[uwp] ERRO: {r.Message}");
            await ProbeUwpCoreAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _appLog.Write(AppLogLevel.Error, "install.uwp", "InstallUwpBundleAsync falhou.", ex);
            AppendInstallLog($"[uwp] ERRO interno: {ex.Message}");
        }
        finally { IsLoading = false; }
    }

    // -- Selecao de pacotes --

    public void OnCatalogPickSelectionChanged(WingetCatalogPickRowViewModel row)
    {
        if (row.IsSelected)
            _pendingInstall[row.Id] = new PendingInstallEntry(row.Name, row.Id, row.Version, row.Source, row.InstallationOrigin);
        else
            _pendingInstall.Remove(row.Id);

        RebuildInstallPlanRows();
        OnPropertyChanged(nameof(HasPendingInstalls));
        CommandManager.InvalidateRequerySuggested();
    }

    private void RebuildInstallPlanRows()
    {
        WingetSearchRows.Clear();
        foreach (WingetSearchItem row in _installTabInstalledItems)
        {
            WingetSearchRows.Add(new WingetSearchRowViewModel(
                row.Name, row.Id, row.Version, row.Source ?? string.Empty, row.InstallationOrigin, isMutedInstalled: true));
        }

        foreach (PendingInstallEntry e in _pendingInstall.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (_installedIdsForInstallTab.Contains(e.Id)) continue;
            WingetSearchRows.Add(new WingetSearchRowViewModel(
                e.Name, e.Id, e.Version, e.Source, e.InstallationOrigin, isMutedInstalled: false));
        }
    }

    // -- Listagem de instalados --

    private async Task LoadInstalledWingetPackagesForInstallTabAsync(bool showLoadingOverlay = true)
    {
        if (showLoadingOverlay)
        {
            IsLoading = true;
            LoadingMessage = "Listando programas (winget / Loja)...";
        }

        try
        {
            _installTabInstalledItems.Clear();
            _installedIdsForInstallTab.Clear();
            var outcome = await _wingetSearch.ListInstalledAsync(500).ConfigureAwait(true);
            if (!outcome.Success)
            {
                WingetSearchRows.Clear();
                AppendInstallLog($"[winget instalados] {outcome.Message}");
                if (!string.IsNullOrEmpty(outcome.FailureDetail))
                    _appLog.Write(AppLogLevel.Warning, "install.winget.list", outcome.Message,
                        data: new Dictionary<string, string> { ["saidaWinget"] = outcome.FailureDetail });
                return;
            }

            foreach (WingetSearchItem row in outcome.Items)
            {
                if (!WingetInstallationOrigin.IsEligibleForInstallTab(row.Source, row.Id, row.InstallationOrigin))
                    continue;
                _installTabInstalledItems.Add(row);
                _installedIdsForInstallTab.Add(row.Id);
            }

            foreach (string id in _installedIdsForInstallTab)
                _pendingInstall.Remove(id);

            foreach (WingetCatalogPickRowViewModel pick in WingetCatalogSearchRows)
                pick.SetSelectedSilent(_pendingInstall.ContainsKey(pick.Id));

            RebuildInstallPlanRows();
            OnPropertyChanged(nameof(HasPendingInstalls));
            CommandManager.InvalidateRequerySuggested();
            AppendInstallLog($"[winget instalados] {outcome.Message} (filtrado: catálogo winget e Loja Microsoft.)");
        }
        catch (Exception ex)
        {
            _appLog.Write(AppLogLevel.Error, "install.winget.list", "LoadInstalledWingetPackagesForInstallTabAsync falhou.", ex);
            AppendInstallLog($"[winget instalados] ERRO interno: {ex.Message}");
        }
        finally
        {
            if (showLoadingOverlay) IsLoading = false;
        }
    }

    private async Task LoadInstalledPackagesForUninstallTabAsync()
    {
        IsLoading = true;
        LoadingMessage = "Listando todos os pacotes (winget list)...";
        try
        {
            WingetCatalogSearchRows.Clear();
            WingetSearchRows.Clear();
            var outcome = await _wingetSearch.ListInstalledAsync(500).ConfigureAwait(true);
            if (!outcome.Success)
            {
                AppendInstallLog($"[desinstalar listagem] {outcome.Message}");
                if (!string.IsNullOrEmpty(outcome.FailureDetail))
                    _appLog.Write(AppLogLevel.Warning, "install.winget.list.full", outcome.Message,
                        data: new Dictionary<string, string> { ["saidaWinget"] = outcome.FailureDetail });
                return;
            }

            foreach (var row in outcome.Items)
                WingetSearchRows.Add(new WingetSearchRowViewModel(row.Name, row.Id, row.Version, row.Source ?? string.Empty, row.InstallationOrigin));
            AppendInstallLog($"[desinstalar listagem] {outcome.Message}");
        }
        catch (Exception ex)
        {
            _appLog.Write(AppLogLevel.Error, "install.winget.list.full", "LoadInstalledPackagesForUninstallTabAsync falhou.", ex);
            AppendInstallLog($"[desinstalar listagem] ERRO interno: {ex.Message}");
        }
        finally { IsLoading = false; }
    }

    // -- Pesquisa no repositorio --

    private async Task SearchWingetCatalogAsync()
    {
        if (string.IsNullOrWhiteSpace(WingetSearchQuery))
        {
            await LoadRecommendationsIntoSearchGridAsync();
            return;
        }

        IsLoading = true;
        LoadingMessage = "Pesquisando no repositorio winget...";
        try
        {
            _repositorySearchRows.Clear();
            var outcome = await _wingetSearch.SearchAsync(WingetSearchQuery, 200).ConfigureAwait(true);
            if (!outcome.Success)
            {
                AppendInstallLog($"[winget pesquisa] {outcome.Message}");
                if (!string.IsNullOrEmpty(outcome.FailureDetail))
                    _appLog.Write(AppLogLevel.Warning, "install.winget.search", outcome.Message,
                        data: new Dictionary<string, string> { ["saidaWinget"] = outcome.FailureDetail });
                return;
            }

            foreach (WingetSearchItem row in outcome.Items)
            {
                bool selected = _pendingInstall.ContainsKey(row.Id);
                _repositorySearchRows.Add(new WingetCatalogPickRowViewModel(
                    this, row.Name, row.Id, row.Version, row.Source ?? string.Empty, row.InstallationOrigin, selected));
            }

            IsShowingOfflineList = false;
            InstallCatalogModeLabel = "Resultados do repositório";
            SyncVisibleCatalogRows(_repositorySearchRows);
            AppendInstallLog($"[winget pesquisa] {outcome.Message}");
        }
        catch (Exception ex)
        {
            _appLog.Write(AppLogLevel.Error, "install.winget.search", "SearchWingetCatalogAsync falhou.", ex);
            AppendInstallLog($"[winget pesquisa] ERRO interno: {ex.Message}");
        }
        finally { IsLoading = false; }
    }

    // -- Instalacao em lote --

    private async Task InstallPendingPackagesAsync()
    {
        List<PendingInstallEntry> toInstall = _pendingInstall.Values
            .Where(p => !_installedIdsForInstallTab.Contains(p.Id)).ToList();
        if (toInstall.Count == 0) return;

        _installCts?.Dispose();
        _installCts = new CancellationTokenSource();
        CancellationToken token = _installCts.Token;

        _succeededInstalls.Clear();
        _failedInstalls.Clear();
        _retryCandidates.Clear();

        IsInstallingPackages = true;
        AppendInstallLog($"[instalar] Lote: {toInstall.Count} pacote(s) (pode cancelar durante a execução).");
        bool wasCancelled = false;
        try
        {
            foreach (PendingInstallEntry p in toInstall)
            {
                token.ThrowIfCancellationRequested();
                AppendInstallLog($"[instalar] Iniciando: {p.Id} ...");
                WingetInstallOutcome outcome = await _wingetPackageInstall
                    .InstallAsync(p.Id, string.IsNullOrWhiteSpace(p.Source) ? null : p.Source, token)
                    .ConfigureAwait(true);
                if (outcome.IsCancelled)
                {
                    AppendInstallLog("[instalar] Cancelado — interrompendo o lote.");
                    wasCancelled = true;
                    break;
                }

                if (outcome.Success)
                {
                    AppendInstallLog($"[instalar] OK {p.Id}: {outcome.Message}");
                    _succeededInstalls.Add(new InstallBatchResultEntry(p.Name, p.Id, p.Source, true, outcome.Message));
                }
                else
                {
                    AppendInstallLog($"[instalar] FALHA {p.Id}: {outcome.Message}");
                    _failedInstalls.Add(new InstallBatchResultEntry(p.Name, p.Id, p.Source, false, outcome.Message, outcome.FailureDetail));
                    if (!string.IsNullOrEmpty(outcome.FailureDetail))
                        _appLog.Write(AppLogLevel.Warning, "install.winget.install", outcome.Message,
                            data: new Dictionary<string, string> { ["detalhe"] = outcome.FailureDetail });
                }
            }

            await LoadInstalledWingetPackagesForInstallTabAsync(showLoadingOverlay: false).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            wasCancelled = true;
            AppendInstallLog("[instalar] Cancelado pelo utilizador.");
            await LoadInstalledWingetPackagesForInstallTabAsync(showLoadingOverlay: false).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _appLog.Write(AppLogLevel.Error, "install.winget.install", "InstallPendingPackagesAsync falhou.", ex);
            AppendInstallLog($"[instalar] ERRO interno: {ex.Message}");
        }
        finally
        {
            IsInstallingPackages = false;
            _installCts?.Dispose();
            _installCts = null;
            OnPropertyChanged(nameof(HasPendingInstalls));
            CommandManager.InvalidateRequerySuggested();
        }

        if (!wasCancelled && _failedInstalls.Count > 0)
            await ResolveSimilarityCandidatesAsync();

        if (_succeededInstalls.Count > 0 || _failedInstalls.Count > 0)
        {
            OnPropertyChanged(nameof(BatchSucceededCount));
            OnPropertyChanged(nameof(BatchFailedCount));
            ShowRetrySummary = true;
        }
    }

    // -- Retry com similaridade --

    private async Task ResolveSimilarityCandidatesAsync()
    {
        AppendInstallLog($"[retry] Resolvendo candidatos por similaridade para {_failedInstalls.Count} falha(s)...");
        foreach (var fail in _failedInstalls)
        {
            try
            {
                var outcome = await _wingetSearch.SearchAsync(fail.Name, 20).ConfigureAwait(true);
                if (!outcome.Success || outcome.Items.Count == 0)
                {
                    _retryCandidates.Add(new RetryCandidateViewModel(fail.Name, fail.Id, string.Empty, string.Empty, string.Empty, 0));
                    continue;
                }

                var ranked = outcome.Items
                    .Select(item => (item, Score: WingetSearchRelevance.ScoreAgainstQuery(fail.Name, item)))
                    .Where(x => x.Score > 0 && !string.Equals(x.item.Id, fail.Id, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => x.Score)
                    .FirstOrDefault();

                if (ranked.item is not null)
                {
                    _retryCandidates.Add(new RetryCandidateViewModel(
                        fail.Name, fail.Id, ranked.item.Name, ranked.item.Id, ranked.item.Source ?? "winget", ranked.Score));
                    AppendInstallLog($"[retry] {fail.Name} → sugestão: {ranked.item.Name} ({ranked.item.Id}) score={ranked.Score}");
                }
                else
                {
                    _retryCandidates.Add(new RetryCandidateViewModel(fail.Name, fail.Id, string.Empty, string.Empty, string.Empty, 0));
                    AppendInstallLog($"[retry] {fail.Name} → sem candidato alternativo encontrado.");
                }
            }
            catch (Exception ex)
            {
                _appLog.Write(AppLogLevel.Warning, "install.retry.resolve", $"Falha ao buscar similares para {fail.Id}", ex);
                _retryCandidates.Add(new RetryCandidateViewModel(fail.Name, fail.Id, string.Empty, string.Empty, string.Empty, 0));
            }
        }
    }

    private async Task RetryAllSuggestedAsync()
    {
        ShowRetrySummary = false;
        var toRetry = _retryCandidates.Where(c => c.HasSuggestion && c.IsApproved).ToList();
        if (toRetry.Count == 0)
        {
            AppendInstallLog("[retry] Nenhum candidato aprovado para retry.");
            return;
        }

        _installCts?.Dispose();
        _installCts = new CancellationTokenSource();
        CancellationToken token = _installCts.Token;
        IsInstallingPackages = true;

        AppendInstallLog($"[retry] Tentando {toRetry.Count} candidato(s) sugeridos...");
        try
        {
            foreach (var candidate in toRetry)
            {
                token.ThrowIfCancellationRequested();
                candidate.RetryStatus = "Instalando...";
                AppendInstallLog($"[retry] {candidate.OriginalName} → tentando {candidate.SuggestedId}...");
                var outcome = await _wingetPackageInstall
                    .InstallAsync(candidate.SuggestedId, candidate.SuggestedSource, token)
                    .ConfigureAwait(true);

                if (outcome.IsCancelled) { candidate.RetryStatus = "Cancelado"; AppendInstallLog("[retry] Cancelado — interrompendo retry."); break; }
                if (outcome.Success) { candidate.RetryStatus = "OK"; AppendInstallLog($"[retry] OK {candidate.SuggestedId}: {outcome.Message}"); }
                else { candidate.RetryStatus = "Falhou"; AppendInstallLog($"[retry] FALHA {candidate.SuggestedId}: {outcome.Message}"); }
            }
            await LoadInstalledWingetPackagesForInstallTabAsync(showLoadingOverlay: false).ConfigureAwait(true);
        }
        catch (OperationCanceledException) { AppendInstallLog("[retry] Cancelado pelo utilizador."); }
        finally
        {
            IsInstallingPackages = false;
            _installCts?.Dispose();
            _installCts = null;
            OnPropertyChanged(nameof(HasPendingInstalls));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private async Task RetryOneByOneAsync()
    {
        ShowRetrySummary = false;
        var toRetry = _retryCandidates.Where(c => c.HasSuggestion).ToList();
        if (toRetry.Count == 0)
        {
            AppendInstallLog("[retry] Nenhum candidato com sugestão disponível.");
            return;
        }

        _installCts?.Dispose();
        _installCts = new CancellationTokenSource();
        CancellationToken token = _installCts.Token;
        IsInstallingPackages = true;

        AppendInstallLog($"[retry] Modo item a item: {toRetry.Count} candidato(s).");
        try
        {
            foreach (var candidate in toRetry)
            {
                token.ThrowIfCancellationRequested();
                if (!candidate.IsApproved) { candidate.RetryStatus = "Ignorado"; AppendInstallLog($"[retry] {candidate.OriginalName} → ignorado pelo utilizador."); continue; }
                candidate.RetryStatus = "Instalando...";
                AppendInstallLog($"[retry] {candidate.OriginalName} → tentando {candidate.SuggestedId}...");
                var outcome = await _wingetPackageInstall
                    .InstallAsync(candidate.SuggestedId, candidate.SuggestedSource, token)
                    .ConfigureAwait(true);

                if (outcome.IsCancelled) { candidate.RetryStatus = "Cancelado"; AppendInstallLog("[retry] Cancelado — interrompendo retry."); break; }
                if (outcome.Success) { candidate.RetryStatus = "OK"; AppendInstallLog($"[retry] OK {candidate.SuggestedId}: {outcome.Message}"); }
                else { candidate.RetryStatus = "Falhou"; AppendInstallLog($"[retry] FALHA {candidate.SuggestedId}: {outcome.Message}"); }
            }
            await LoadInstalledWingetPackagesForInstallTabAsync(showLoadingOverlay: false).ConfigureAwait(true);
        }
        catch (OperationCanceledException) { AppendInstallLog("[retry] Cancelado pelo utilizador."); }
        finally
        {
            IsInstallingPackages = false;
            _installCts?.Dispose();
            _installCts = null;
            OnPropertyChanged(nameof(HasPendingInstalls));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    // -- Factory --

    public static MainWindowViewModel FromSnapshot(
        RuntimeDiagnosticsSnapshot snapshot,
        IWingetProvisioningService wingetProvisioning,
        IUwpAppInstallerProvisioningService uwpProvisioning,
        IWingetSearchService wingetSearch,
        IWingetPackageInstallService wingetPackageInstall,
        IAppJsonLog appLog)
    {
        return new MainWindowViewModel(
            title: $"BananaSuisa v{snapshot.AppVersion}",
            subtitle: "Instalacao, remocao e manutencao do ecossistema winget.",
            appVersion: snapshot.AppVersion,
            wingetPath: snapshot.WingetPath ?? "Nao encontrado",
            generatedAt: snapshot.GeneratedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"),
            wingetProvisioning: wingetProvisioning,
            uwpProvisioning: uwpProvisioning,
            wingetSearch: wingetSearch,
            wingetPackageInstall: wingetPackageInstall,
            appLog: appLog);
    }
}
