using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows.Input;
using BananaSuisa.App.Views;
using BananaSuisa.Core.Diagnostics;
using BananaSuisa.Core.Logging;
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
    private string _installActivityLog = string.Empty;
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

    public string InstallActivityLog
    {
        get => _installActivityLog;
        set => SetProperty(ref _installActivityLog, value);
    }

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
        set => SetProperty(ref _wingetSearchQuery, value);
    }

    public ObservableCollection<WingetSearchRowViewModel> WingetSearchRows { get; } = new();

    public ObservableCollection<WingetCatalogPickRowViewModel> WingetCatalogSearchRows { get; } = new();

    public bool HasPendingInstalls =>
        _pendingInstall.Keys.Any(id => !_installedIdsForInstallTab.Contains(id));

    /// <summary>True enquanto <c>winget install</c> corre em lote (sem overlay global; log continua visível).</summary>
    public bool IsInstallingPackages
    {
        get => _isInstallingPackages;
        private set
        {
            if (SetProperty(ref _isInstallingPackages, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
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

    private MainWindowViewModel(
        string title,
        string subtitle,
        string appVersion,
        string baseDirectory,
        string projectRoot,
        string resourcesRoot,
        string memoryRoot,
        string dataRoot,
        string configPath,
        string configurationSourcePath,
        string configurationSummary,
        string searchSummary,
        string searchPreviewQuery,
        IReadOnlyList<SearchPreviewItemViewModel> searchPreviewItems,
        string catalogSummary,
        string catalogSearchSummary,
        string catalogSearchPreviewQuery,
        IReadOnlyList<SearchPreviewItemViewModel> catalogSearchPreviewItems,
        string wingetPath,
        string workspaceSummary,
        IReadOnlyList<BootstrapPathRowViewModel> bootstrapPathRows,
        IReadOnlyList<DiagnosticCheckViewModel> workspaceItems,
        string generatedAt,
        IReadOnlyList<DiagnosticCheckViewModel> checks,
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
        BaseDirectory = baseDirectory;
        ProjectRoot = projectRoot;
        ResourcesRoot = resourcesRoot;
        MemoryRoot = memoryRoot;
        DataRoot = dataRoot;
        ConfigPath = configPath;
        ConfigurationSourcePath = configurationSourcePath;
        ConfigurationSummary = configurationSummary;
        SearchSummary = searchSummary;
        SearchPreviewQuery = searchPreviewQuery;
        SearchPreviewItems = searchPreviewItems;
        CatalogSummary = catalogSummary;
        CatalogSearchSummary = catalogSearchSummary;
        CatalogSearchPreviewQuery = catalogSearchPreviewQuery;
        CatalogSearchPreviewItems = catalogSearchPreviewItems;
        WingetPath = wingetPath;
        WorkspaceSummary = workspaceSummary;
        BootstrapPathRows = bootstrapPathRows;
        WorkspaceItems = workspaceItems;
        GeneratedAt = generatedAt;
        Checks = checks;

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

        CurrentView = new DashboardView { DataContext = this };
    }

    private void Navigate(object? parameter)
    {
        if (parameter is not string viewName)
        {
            return;
        }

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

        CurrentView = viewName switch
        {
            "Dashboard" => new DashboardView { DataContext = this },
            "Catalog" => new CatalogView { DataContext = this },
            "Logs" => new LogsView { DataContext = this },
            "Settings" => new SettingsView { DataContext = this },
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
        if (parameter is not string key)
        {
            return;
        }

        InstallSubKey = key;
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
        }
        else if (key == "InstallUninstall")
        {
            _ = LoadInstalledPackagesForUninstallTabAsync();
        }
    }

    private void AppendInstallLog(string line)
    {
        InstallActivityLog += $"[{DateTime.Now:HH:mm:ss}] {line}\r\n";
        _appLog.Write(AppLogLevel.Information, "install.ui", line);
    }

    private async Task ProbeWingetAsync()
    {
        IsLoading = true;
        LoadingMessage = "Verificando winget...";
        try
        {
            await ProbeWingetCoreAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _appLog.Write(AppLogLevel.Error, "install.winget", "ProbeWingetAsync falhou.", ex);
            AppendInstallLog($"[winget] ERRO interno: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ProbeWingetCoreAsync()
    {
        var r = await _wingetProvisioning.ProbeAsync().ConfigureAwait(true);
        WingetProbeSummary = r.Summary;
        AppendInstallLog($"[winget] {r.Summary}");
        if (r.IsHealthy)
        {
            AppendInstallLog($"[winget] Integridade: OK (source list exit={r.SourceListExitCode}).");
        }
        else
        {
            AppendInstallLog("[winget] Integridade: possivel falha — tente Instalar bundle ou Reinstalar.");
        }
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
        finally
        {
            IsLoading = false;
        }
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
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ProbeUwpAsync()
    {
        IsLoading = true;
        LoadingMessage = "Verificando pacotes App Installer e Loja...";
        try
        {
            await ProbeUwpCoreAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _appLog.Write(AppLogLevel.Error, "install.uwp", "ProbeUwpAsync falhou.", ex);
            AppendInstallLog($"[uwp] ERRO interno: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
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
        finally
        {
            IsLoading = false;
        }
    }

    public void OnCatalogPickSelectionChanged(WingetCatalogPickRowViewModel row)
    {
        if (row.IsSelected)
        {
            _pendingInstall[row.Id] = new PendingInstallEntry(
                row.Name,
                row.Id,
                row.Version,
                row.Source,
                row.InstallationOrigin);
        }
        else
        {
            _pendingInstall.Remove(row.Id);
        }

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
                row.Name,
                row.Id,
                row.Version,
                row.Source ?? string.Empty,
                row.InstallationOrigin,
                isMutedInstalled: true));
        }

        foreach (PendingInstallEntry e in _pendingInstall.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (_installedIdsForInstallTab.Contains(e.Id))
            {
                continue;
            }

            WingetSearchRows.Add(new WingetSearchRowViewModel(
                e.Name,
                e.Id,
                e.Version,
                e.Source,
                e.InstallationOrigin,
                isMutedInstalled: false));
        }
    }

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
                {
                    _appLog.Write(
                        AppLogLevel.Warning,
                        "install.winget.list",
                        outcome.Message,
                        data: new Dictionary<string, string> { ["saidaWinget"] = outcome.FailureDetail });
                }

                return;
            }

            foreach (WingetSearchItem row in outcome.Items)
            {
                if (!WingetInstallationOrigin.IsEligibleForInstallTab(row.Source, row.Id, row.InstallationOrigin))
                {
                    continue;
                }

                _installTabInstalledItems.Add(row);
                _installedIdsForInstallTab.Add(row.Id);
            }

            foreach (string id in _installedIdsForInstallTab)
            {
                _pendingInstall.Remove(id);
            }

            foreach (WingetCatalogPickRowViewModel pick in WingetCatalogSearchRows)
            {
                pick.SetSelectedSilent(_pendingInstall.ContainsKey(pick.Id));
            }

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
            if (showLoadingOverlay)
            {
                IsLoading = false;
            }
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
                {
                    _appLog.Write(
                        AppLogLevel.Warning,
                        "install.winget.list.full",
                        outcome.Message,
                        data: new Dictionary<string, string> { ["saidaWinget"] = outcome.FailureDetail });
                }

                return;
            }

            foreach (var row in outcome.Items)
            {
                WingetSearchRows.Add(new WingetSearchRowViewModel(row.Name, row.Id, row.Version, row.Source ?? string.Empty, row.InstallationOrigin));
            }

            AppendInstallLog($"[desinstalar listagem] {outcome.Message}");
        }
        catch (Exception ex)
        {
            _appLog.Write(AppLogLevel.Error, "install.winget.list.full", "LoadInstalledPackagesForUninstallTabAsync falhou.", ex);
            AppendInstallLog($"[desinstalar listagem] ERRO interno: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SearchWingetCatalogAsync()
    {
        IsLoading = true;
        LoadingMessage = "Pesquisando no repositorio winget...";
        try
        {
            WingetCatalogSearchRows.Clear();
            var outcome = await _wingetSearch.SearchAsync(WingetSearchQuery, 200).ConfigureAwait(true);
            if (!outcome.Success)
            {
                AppendInstallLog($"[winget pesquisa] {outcome.Message}");
                if (!string.IsNullOrEmpty(outcome.FailureDetail))
                {
                    _appLog.Write(
                        AppLogLevel.Warning,
                        "install.winget.search",
                        outcome.Message,
                        data: new Dictionary<string, string> { ["saidaWinget"] = outcome.FailureDetail });
                }

                return;
            }

            foreach (WingetSearchItem row in outcome.Items)
            {
                bool selected = _pendingInstall.ContainsKey(row.Id);
                WingetCatalogSearchRows.Add(new WingetCatalogPickRowViewModel(
                    this,
                    row.Name,
                    row.Id,
                    row.Version,
                    row.Source ?? string.Empty,
                    row.InstallationOrigin,
                    selected));
            }

            AppendInstallLog($"[winget pesquisa] {outcome.Message}");
        }
        catch (Exception ex)
        {
            _appLog.Write(AppLogLevel.Error, "install.winget.search", "SearchWingetCatalogAsync falhou.", ex);
            AppendInstallLog($"[winget pesquisa] ERRO interno: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task InstallPendingPackagesAsync()
    {
        List<PendingInstallEntry> toInstall = _pendingInstall.Values
            .Where(p => !_installedIdsForInstallTab.Contains(p.Id))
            .ToList();
        if (toInstall.Count == 0)
        {
            return;
        }

        _installCts?.Dispose();
        _installCts = new CancellationTokenSource();
        CancellationToken token = _installCts.Token;

        IsInstallingPackages = true;
        AppendInstallLog($"[instalar] Lote: {toInstall.Count} pacote(s) (pode cancelar durante a execução).");
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
                    break;
                }

                if (outcome.Success)
                {
                    AppendInstallLog($"[instalar] OK {p.Id}: {outcome.Message}");
                }
                else
                {
                    AppendInstallLog($"[instalar] FALHA {p.Id}: {outcome.Message}");
                    if (!string.IsNullOrEmpty(outcome.FailureDetail))
                    {
                        _appLog.Write(
                            AppLogLevel.Warning,
                            "install.winget.install",
                            outcome.Message,
                            data: new Dictionary<string, string> { ["detalhe"] = outcome.FailureDetail });
                    }
                }
            }

            await LoadInstalledWingetPackagesForInstallTabAsync(showLoadingOverlay: false).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
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
    }

    public string Title { get; }

    public string Subtitle { get; }

    public string AppVersionLabel { get; }

    public string BaseDirectory { get; }

    public string ProjectRoot { get; }

    public string ResourcesRoot { get; }

    public string MemoryRoot { get; }

    public string DataRoot { get; }

    public string ConfigPath { get; }

    public string ConfigurationSourcePath { get; }

    public string ConfigurationSummary { get; }

    public string SearchSummary { get; }

    public string SearchPreviewQuery { get; }

    public IReadOnlyList<SearchPreviewItemViewModel> SearchPreviewItems { get; }

    public string CatalogSummary { get; }

    public string CatalogSearchSummary { get; }

    public string CatalogSearchPreviewQuery { get; }

    public IReadOnlyList<SearchPreviewItemViewModel> CatalogSearchPreviewItems { get; }

    public string WingetPath { get; }

    public string WorkspaceSummary { get; }

    public IReadOnlyList<BootstrapPathRowViewModel> BootstrapPathRows { get; }

    public IReadOnlyList<DiagnosticCheckViewModel> WorkspaceItems { get; }

    public string GeneratedAt { get; }

    public IReadOnlyList<DiagnosticCheckViewModel> Checks { get; }

    public static MainWindowViewModel FromSnapshot(
        RuntimeDiagnosticsSnapshot snapshot,
        IWingetProvisioningService wingetProvisioning,
        IUwpAppInstallerProvisioningService uwpProvisioning,
        IWingetSearchService wingetSearch,
        IWingetPackageInstallService wingetPackageInstall,
        IAppJsonLog appLog)
    {
        IReadOnlyList<DiagnosticCheckViewModel> checks = snapshot.Checks
            .Select(check => new DiagnosticCheckViewModel(check.Name, check.IsHealthy, check.Detail))
            .ToArray();
        IReadOnlyList<DiagnosticCheckViewModel> workspaceItems = snapshot.WorkspaceBootstrapResult?.Items
            .Select(item => new DiagnosticCheckViewModel(item.Name, item.IsHealthy, item.Detail))
            .ToArray()
            ?? [];
        IReadOnlyList<SearchPreviewItemViewModel> searchPreviewItems = snapshot.ConfigurationSearchPreview?.PreviewMatches
            .Select(match => new SearchPreviewItemViewModel(match.Kind, match.DisplayText, match.Detail))
            .ToArray()
            ?? [];
        IReadOnlyList<SearchPreviewItemViewModel> catalogSearchPreviewItems = snapshot.CatalogSearchPreview?.PreviewItems
            .Select(item => new SearchPreviewItemViewModel(
                kind: item.SourceName,
                title: item.Name,
                detail: $"{item.PackageId} | {item.Category} | {(item.IsEssential ? "Essencial" : "Opcional")}"))
            .ToArray()
            ?? [];

        string projectRoot = snapshot.WorkspacePaths?.ProjectRoot ?? "Nao localizado";
        string resourcesRoot = snapshot.WorkspacePaths?.ResourcesRoot ?? "Nao localizado";
        string memoryRoot = snapshot.WorkspacePaths?.MemoryRoot ?? "Nao localizado";
        string dataRoot = snapshot.WorkspaceBootstrapResult?.Paths.DataRoot ?? "Nao localizado";
        string configPath = snapshot.WorkspaceBootstrapResult?.Paths.ConfigPath ?? "Nao localizado";
        string configurationSourcePath = snapshot.ConfigurationLoadResult?.SourcePath ?? "Nao carregado";
        string configurationSummary = snapshot.ConfigurationLoadResult?.Summary ?? "Configuracao nao carregada.";
        string searchSummary = snapshot.ConfigurationSearchPreview?.Summary ?? "Busca ainda nao preparada.";
        string? rawSearchPreviewQuery = snapshot.ConfigurationSearchPreview?.PreviewQuery;
        string searchPreviewQuery = string.IsNullOrWhiteSpace(rawSearchPreviewQuery)
            ? "Consulta piloto indisponivel"
            : rawSearchPreviewQuery;
        string catalogSummary = snapshot.CatalogLoadResult?.Summary ?? "Catalogos nao carregados.";
        string catalogSearchSummary = snapshot.CatalogSearchPreview?.Summary ?? "Busca do catalogo ainda nao preparada.";
        string? rawCatalogSearchPreviewQuery = snapshot.CatalogSearchPreview?.PreviewQuery;
        string catalogSearchPreviewQuery = string.IsNullOrWhiteSpace(rawCatalogSearchPreviewQuery)
            ? "Consulta piloto indisponivel"
            : rawCatalogSearchPreviewQuery;
        string workspaceSummary = snapshot.WorkspaceBootstrapResult is null
            ? "Workspace nao inicializado."
            : $"Pastas criadas: {snapshot.WorkspaceBootstrapResult.CreatedDirectoryCount} | Arquivos sincronizados: {snapshot.WorkspaceBootstrapResult.SynchronizedFileCount}";

        string wingetPath = snapshot.WingetPath ?? "Nao encontrado";
        IReadOnlyList<BootstrapPathRowViewModel> bootstrapPathRows =
        [
            new BootstrapPathRowViewModel("Base directory", snapshot.BaseDirectory),
            new BootstrapPathRowViewModel("Project root", projectRoot),
            new BootstrapPathRowViewModel("Resources root", resourcesRoot),
            new BootstrapPathRowViewModel("Memory root", memoryRoot),
            new BootstrapPathRowViewModel("Winget", wingetPath)
        ];

        return new MainWindowViewModel(
            title: $"BananaSuisa .NET Bootstrap v{snapshot.AppVersion}",
            subtitle: "Primeiro esqueleto WPF com diagnostico de runtime, workspace e busca inicial sobre configuracao e catalogo.",
            appVersion: snapshot.AppVersion,
            baseDirectory: snapshot.BaseDirectory,
            projectRoot: projectRoot,
            resourcesRoot: resourcesRoot,
            memoryRoot: memoryRoot,
            dataRoot: dataRoot,
            configPath: configPath,
            configurationSourcePath: configurationSourcePath,
            configurationSummary: configurationSummary,
            searchSummary: searchSummary,
            searchPreviewQuery: searchPreviewQuery,
            searchPreviewItems: searchPreviewItems,
            catalogSummary: catalogSummary,
            catalogSearchSummary: catalogSearchSummary,
            catalogSearchPreviewQuery: catalogSearchPreviewQuery,
            catalogSearchPreviewItems: catalogSearchPreviewItems,
            wingetPath: wingetPath,
            workspaceSummary: workspaceSummary,
            bootstrapPathRows: bootstrapPathRows,
            workspaceItems: workspaceItems,
            generatedAt: snapshot.GeneratedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"),
            checks: checks,
            wingetProvisioning: wingetProvisioning,
            uwpProvisioning: uwpProvisioning,
            wingetSearch: wingetSearch,
            wingetPackageInstall: wingetPackageInstall,
            appLog: appLog);
    }
}
