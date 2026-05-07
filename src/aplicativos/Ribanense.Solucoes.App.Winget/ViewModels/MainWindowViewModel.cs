using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Ribanense.Solucoes.App.Winget.Services;
using Ribanense.Solucoes.App.Winget.Services.Search;
using Ribanense.Solucoes.App.Winget.Services.Sources;
using Ribanense.Solucoes.PluginSDK.Logging;
using Ribanense.Solucoes.UI;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Winget.ViewModels;

public enum AppTab
{
    Search,
    Installed,
    Sources,
    Module
}

public sealed class MainWindowViewModel : ObservableObject, IPackageRowHost
{
    private readonly IWingetInstallService _installer;
    private readonly IAppJsonLog _log;
    private readonly IWingetLocator _locator;

    public MainWindowViewModel(
        ISearchEnhancer search,
        IWingetListService list,
        IWingetInstallService installer,
        IWingetLocator locator,
        IWingetSourceService sources,
        IAppAliasCatalog aliasCatalog,
        IAppJsonLog log)
    {
        _installer = installer;
        _locator = locator;
        _log = log;

        SearchTab = new SearchViewModel(search, aliasCatalog, this);
        InstalledTab = new InstalledViewModel(list, this);
        SourcesTab = new SourcesViewModel(sources, log, DispatcherAppend);

        SelectSearchCommand = new RelayCommand(() => CurrentTab = AppTab.Search);
        SelectInstalledCommand = new RelayCommand(() => CurrentTab = AppTab.Installed);
        SelectSourcesCommand = new RelayCommand(() => CurrentTab = AppTab.Sources);
        SelectModuleCommand = new RelayCommand(() => CurrentTab = AppTab.Module);
        ClearLogCommand = new RelayCommand(() => LogLines.Clear());
        CopyLogCommand = new RelayCommand(() => LogLinesClipboard.CopyOrWarn(LogLines, ProductName));
    }

    public SearchViewModel SearchTab { get; }
    public InstalledViewModel InstalledTab { get; }
    public SourcesViewModel SourcesTab { get; }

    /// <summary>
    /// Aba "Modulo" sera preenchida na Fase B do plano (diagnostico + reparo).
    /// Por enquanto e um hook opcional injetavel via composition root.
    /// </summary>
    public ModuleViewModel? ModuleTab { get; init; }

    public ObservableCollection<string> LogLines { get; } = new();

    private AppTab _currentTab = AppTab.Search;
    public AppTab CurrentTab
    {
        get => _currentTab;
        set
        {
            if (SetProperty(ref _currentTab, value))
            {
                OnPropertyChanged(nameof(IsSearchActive));
                OnPropertyChanged(nameof(IsInstalledActive));
                OnPropertyChanged(nameof(IsSourcesActive));
                OnPropertyChanged(nameof(IsModuleActive));
                _ = OnTabActivatedAsync(value);
            }
        }
    }

    public bool IsSearchActive => CurrentTab == AppTab.Search;
    public bool IsInstalledActive => CurrentTab == AppTab.Installed;
    public bool IsSourcesActive => CurrentTab == AppTab.Sources;
    public bool IsModuleActive => CurrentTab == AppTab.Module;

    public string ProductName => "Gestor WinGet";

    private string _wingetStatus = "Verificando winget...";
    public string WingetStatus
    {
        get => _wingetStatus;
        set => SetProperty(ref _wingetStatus, value);
    }

    public ICommand SelectSearchCommand { get; }
    public ICommand SelectInstalledCommand { get; }
    public ICommand SelectSourcesCommand { get; }
    public ICommand SelectModuleCommand { get; }
    public ICommand ClearLogCommand { get; }
    public ICommand CopyLogCommand { get; }

    public Task BootstrapAsync()
    {
        string? path = _locator.TryLocate();
        WingetStatus = path is null
            ? "winget.exe nao encontrado. Abra a aba Modulo para diagnosticar/reparar."
            : $"winget: {path}";

        AppendLog($"Gestor WinGet iniciado. {WingetStatus}");
        return path is null ? Task.CompletedTask : InstalledTab.RefreshAsync();
    }

    private async Task OnTabActivatedAsync(AppTab tab)
    {
        try
        {
            switch (tab)
            {
                case AppTab.Sources:
                    if (SourcesTab.Rows.Count == 0)
                    {
                        await SourcesTab.ReloadAsync().ConfigureAwait(true);
                    }
                    break;
                case AppTab.Module:
                    if (ModuleTab is not null && ModuleTab.Status is null)
                    {
                        await ModuleTab.InspectAsync().ConfigureAwait(true);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _log.Write(AppLogLevel.Warning, "tab.activate", $"Falha ao ativar aba {tab}.", ex);
        }
    }

    public Task InstallAsync(PackageRowViewModel row) =>
        RunOperationAsync(row, "Instalando", (id, onLine, ct) => _installer.InstallAsync(id, onLine, ct), refreshInstalled: true);

    public Task UninstallAsync(PackageRowViewModel row) =>
        RunOperationAsync(row, "Desinstalando", (id, onLine, ct) => _installer.UninstallAsync(id, onLine, ct), refreshInstalled: true);

    public Task UpgradeAsync(PackageRowViewModel row) =>
        RunOperationAsync(row, "Atualizando", (id, onLine, ct) => _installer.UpgradeAsync(id, onLine, ct), refreshInstalled: true);

    private async Task RunOperationAsync(
        PackageRowViewModel row,
        string verb,
        Func<string, Action<string>, CancellationToken, Task<Domain.WingetRunResult>> op,
        bool refreshInstalled)
    {
        row.IsBusy = true;
        row.Status = $"{verb}...";
        AppendLog($"== {verb} {row.Id} ==");

        try
        {
            var result = await op(row.Id, line => DispatcherAppend(line), CancellationToken.None).ConfigureAwait(true);
            if (result.Success)
            {
                row.Status = $"{verb} concluido.";
                AppendLog($"OK - {row.Id} ({verb.ToLowerInvariant()}).");
                _log.Write(AppLogLevel.Information, "winget.op", $"{verb} OK: {row.Id}");
            }
            else
            {
                row.Status = $"Falhou (codigo {result.ExitCode}).";
                AppendLog($"FALHA (exit={result.ExitCode}) - {row.Id}.");
                _log.Write(AppLogLevel.Warning, "winget.op", $"{verb} falhou: {row.Id} exit={result.ExitCode}");
            }

            if (refreshInstalled)
            {
                await InstalledTab.RefreshAsync().ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            row.Status = $"Erro: {ex.Message}";
            AppendLog($"ERRO - {row.Id}: {ex.Message}");
            _log.Write(AppLogLevel.Error, "winget.op.exception", $"{verb} {row.Id} lancou excecao.", ex);
        }
        finally
        {
            row.IsBusy = false;
        }
    }

    private void DispatcherAppend(string line)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            AppendLog(line);
            return;
        }
        dispatcher.Invoke(() => AppendLog(line));
    }

    private void AppendLog(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        LogLines.Add(line);
        while (LogLines.Count > 400) LogLines.RemoveAt(0);
    }
}
