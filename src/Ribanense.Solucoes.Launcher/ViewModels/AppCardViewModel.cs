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
}

public sealed class AppCardViewModel : ObservableObject
{
    private readonly IAppCardHost _host;

    public AppCardViewModel(CatalogEntry entry, IAppCardHost host)
    {
        Entry = entry;
        _host = host;

        InstallCommand = new AsyncRelayCommand(_ => _host.InstallAsync(this), _ => CanInstall);
        UpdateCommand = new AsyncRelayCommand(_ => _host.UpdateAsync(this), _ => CanUpdate);
        UninstallCommand = new AsyncRelayCommand(_ => _host.UninstallAsync(this), _ => CanUninstall);
        OpenCommand = new RelayCommand(_ => _host.Open(this), _ => CanOpen);
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
            }
        }
    }

    private double _progress;
    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

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

    public ICommand InstallCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand UninstallCommand { get; }
    public ICommand OpenCommand { get; }
}
