using System.Windows;
using System.Windows.Input;
using Ribanense.Solucoes.Launcher.Domain;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.Launcher.ViewModels;

public interface IAppCardHost
{
    Task InstallAsync(AppCardViewModel card);
    Task UpdateAsync(AppCardViewModel card);
    Task UninstallAsync(AppCardViewModel card);
    void Open(AppCardViewModel card);
    Task InstallRuntimeAsync(AppCardViewModel card);
}

public sealed class AppCardViewModel : ObservableObject
{
    private readonly IAppCardHost _host;
    private readonly AsyncRelayCommand _installCommand;
    private readonly AsyncRelayCommand _updateCommand;
    private readonly AsyncRelayCommand _uninstallCommand;
    private readonly RelayCommand _openCommand;
    private readonly AsyncRelayCommand _installRuntimeCommand;

    public AppCardViewModel(CatalogEntry entry, IAppCardHost host)
    {
        Entry = entry;
        _host = host;

        _installCommand = new AsyncRelayCommand(_ => _host.InstallAsync(this), _ => CanInstall);
        _updateCommand = new AsyncRelayCommand(_ => _host.UpdateAsync(this), _ => CanUpdate);
        _uninstallCommand = new AsyncRelayCommand(_ => _host.UninstallAsync(this), _ => CanUninstall);
        _openCommand = new RelayCommand(_ => _host.Open(this), _ => CanOpen);
        _installRuntimeCommand = new AsyncRelayCommand(_ => _host.InstallRuntimeAsync(this), _ => NeedsRuntimeInstall);
    }

    public CatalogEntry Entry { get; }

    public string DisplayName => Entry.DisplayName;
    public string Category => Entry.Category;
    public string Description => Entry.Description;
    public string Id => Entry.Id;

    private InstalledApp? _installed;
    public InstalledApp? Installed
    {
        get => _installed;
        set
        {
            if (SetProperty(ref _installed, value))
            {
                OnPropertyChanged(nameof(InstalledVersion));
                OnPropertyChanged(nameof(CanInstall));
                OnPropertyChanged(nameof(CanUpdate));
                OnPropertyChanged(nameof(CanOpen));
                OnPropertyChanged(nameof(CanUninstall));
                OnPropertyChanged(nameof(StatusLabel));
                RefreshCommands();
            }
        }
    }

    private ReleaseInfo? _latestRelease;
    public ReleaseInfo? LatestRelease
    {
        get => _latestRelease;
        set
        {
            if (SetProperty(ref _latestRelease, value))
            {
                OnPropertyChanged(nameof(LatestVersion));
                OnPropertyChanged(nameof(CanInstall));
                OnPropertyChanged(nameof(CanUpdate));
                OnPropertyChanged(nameof(StatusLabel));
                RefreshCommands();
            }
        }
    }

    private UpdateStatus _status = UpdateStatus.NotInstalled;
    public UpdateStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(CanInstall));
                OnPropertyChanged(nameof(CanUpdate));
                OnPropertyChanged(nameof(CanOpen));
                OnPropertyChanged(nameof(CanUninstall));
                OnPropertyChanged(nameof(StatusLabel));
                RefreshCommands();
            }
        }
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanInstall));
                OnPropertyChanged(nameof(CanUpdate));
                OnPropertyChanged(nameof(CanOpen));
                OnPropertyChanged(nameof(CanUninstall));
                RefreshCommands();
            }
        }
    }

    private double _progress;
    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    private bool _progressIndeterminate;
    public bool ProgressIndeterminate
    {
        get => _progressIndeterminate;
        set => SetProperty(ref _progressIndeterminate, value);
    }

    private string? _activityMessage;
    public string? ActivityMessage
    {
        get => _activityMessage;
        set => SetProperty(ref _activityMessage, value);
    }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    private string? _requiredRuntimeVersion;
    /// <summary>
    /// Preenchido quando <see cref="IAppCardHost.Open"/> detecta que o .NET Desktop Runtime
    /// exigido pelo app nao esta instalado. O Launcher baixa e instala esse runtime.
    /// </summary>
    public string? RequiredRuntimeVersion
    {
        get => _requiredRuntimeVersion;
        set
        {
            if (SetProperty(ref _requiredRuntimeVersion, value))
            {
                OnPropertyChanged(nameof(NeedsRuntimeInstall));
                RefreshCommands();
            }
        }
    }

    public bool NeedsRuntimeInstall => !string.IsNullOrWhiteSpace(RequiredRuntimeVersion);

    public string? InstalledVersion => Installed?.Version;
    public string? LatestVersion => LatestRelease?.Version;

    public bool CanInstall => !IsBusy && Installed is null && LatestRelease is not null;
    public bool CanUpdate => !IsBusy && Installed is not null && Status == UpdateStatus.UpdateAvailable;
    public bool CanOpen => !IsBusy && Installed is not null;
    public bool CanUninstall => !IsBusy && Installed is not null;

    public string StatusLabel => Status switch
    {
        UpdateStatus.NotInstalled => "Não instalado",
        UpdateStatus.UpToDate => $"Atualizado (v{InstalledVersion})",
        UpdateStatus.UpdateAvailable => $"Atualizar: v{InstalledVersion} → v{LatestVersion}",
        UpdateStatus.IncompatibleLauncher => "Requer Launcher mais novo",
        UpdateStatus.CorruptedInstallation => "Instalação inválida",
        UpdateStatus.ReleaseNotFound => "Sem release publicado",
        _ => ""
    };

    public ICommand InstallCommand => _installCommand;
    public ICommand UpdateCommand => _updateCommand;
    public ICommand UninstallCommand => _uninstallCommand;
    public ICommand OpenCommand => _openCommand;
    public ICommand InstallRuntimeCommand => _installRuntimeCommand;

    private void RefreshCommands()
    {
        void Raise()
        {
            _installCommand.RaiseCanExecuteChanged();
            _updateCommand.RaiseCanExecuteChanged();
            _uninstallCommand.RaiseCanExecuteChanged();
            _openCommand.RaiseCanExecuteChanged();
            _installRuntimeCommand.RaiseCanExecuteChanged();
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(Raise);
            return;
        }

        Raise();
    }

}
