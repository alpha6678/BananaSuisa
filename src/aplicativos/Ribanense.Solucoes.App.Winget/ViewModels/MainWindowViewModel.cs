using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Ribanense.Solucoes.App.Winget.Services;
using Ribanense.Solucoes.PluginSDK.Logging;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Winget.ViewModels;

public enum AppTab
{
    Search,
    Installed
}

public sealed class MainWindowViewModel : ObservableObject, IPackageRowHost
{
    private readonly IWingetInstallService _installer;
    private readonly IAppJsonLog _log;
    private readonly IWingetLocator _locator;

    public MainWindowViewModel(
        IWingetSearchService search,
        IWingetListService list,
        IWingetInstallService installer,
        IWingetLocator locator,
        IAppJsonLog log)
    {
        _installer = installer;
        _locator = locator;
        _log = log;

        SearchTab = new SearchViewModel(search, this);
        InstalledTab = new InstalledViewModel(list, this);

        SelectSearchCommand = new RelayCommand(() => CurrentTab = AppTab.Search);
        SelectInstalledCommand = new RelayCommand(() => CurrentTab = AppTab.Installed);
        ClearLogCommand = new RelayCommand(() => LogLines.Clear());
    }

    public SearchViewModel SearchTab { get; }
    public InstalledViewModel InstalledTab { get; }

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
            }
        }
    }

    public bool IsSearchActive => CurrentTab == AppTab.Search;
    public bool IsInstalledActive => CurrentTab == AppTab.Installed;

    public string ProductName => "Gestor WinGet";

    private string _wingetStatus = "Verificando winget...";
    public string WingetStatus
    {
        get => _wingetStatus;
        set => SetProperty(ref _wingetStatus, value);
    }

    public ICommand SelectSearchCommand { get; }
    public ICommand SelectInstalledCommand { get; }
    public ICommand ClearLogCommand { get; }

    public Task BootstrapAsync()
    {
        string? path = _locator.TryLocate();
        WingetStatus = path is null
            ? "winget.exe não encontrado. Instale o App Installer pela Microsoft Store."
            : $"winget: {path}";

        AppendLog($"Gestor WinGet iniciado. {WingetStatus}");
        return path is null ? Task.CompletedTask : InstalledTab.RefreshAsync();
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
                row.Status = $"{verb} concluído.";
                AppendLog($"OK — {row.Id} ({verb.ToLowerInvariant()}).");
                _log.Write(AppLogLevel.Information, "winget.op", $"{verb} OK: {row.Id}");
            }
            else
            {
                row.Status = $"Falhou (código {result.ExitCode}).";
                AppendLog($"FALHA (exit={result.ExitCode}) — {row.Id}.");
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
            AppendLog($"ERRO — {row.Id}: {ex.Message}");
            _log.Write(AppLogLevel.Error, "winget.op.exception", $"{verb} {row.Id} lançou exceção.", ex);
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
