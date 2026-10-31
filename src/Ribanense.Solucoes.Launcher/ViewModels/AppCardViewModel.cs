using System.Diagnostics;
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
}

public sealed class AppCardViewModel : ObservableObject
{
    private readonly IAppCardHost _host;
    private readonly AsyncRelayCommand _installCommand;
    private readonly AsyncRelayCommand _updateCommand;
    private readonly AsyncRelayCommand _uninstallCommand;
    private readonly RelayCommand _openCommand;
    private readonly RelayCommand _openMissingRuntimeCommand;

    public AppCardViewModel(CatalogEntry entry, IAppCardHost host)
    {
        Entry = entry;
        _host = host;

        _installCommand = new AsyncRelayCommand(_ => _host.InstallAsync(this), _ => CanInstall);
        _updateCommand = new AsyncRelayCommand(_ => _host.UpdateAsync(this), _ => CanUpdate);
        _uninstallCommand = new AsyncRelayCommand(_ => _host.UninstallAsync(this), _ => CanUninstall);
        _openCommand = new RelayCommand(_ => _host.Open(this), _ => CanOpen);
        _openMissingRuntimeCommand = new RelayCommand(_ => OpenMissingRuntimeUrl(), _ => HasMissingRuntimeUrl);
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

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    private string? _missingRuntimeUrl;
    /// <summary>
    /// Preenchido quando <see cref="Open"/> detecta que o .NET Desktop Runtime exigido pelo app
    /// nao esta instalado nesta maquina; aponta para a pagina de download correspondente.
    /// </summary>
    public string? MissingRuntimeUrl
    {
        get => _missingRuntimeUrl;
        set
        {
            if (SetProperty(ref _missingRuntimeUrl, value))
            {
                OnPropertyChanged(nameof(HasMissingRuntimeUrl));
                RefreshCommands();
            }
        }
    }

    public bool HasMissingRuntimeUrl => !string.IsNullOrEmpty(MissingRuntimeUrl);

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
    public ICommand OpenMissingRuntimeCommand => _openMissingRuntimeCommand;

    private void RefreshCommands()
    {
        void Raise()
        {
            _installCommand.RaiseCanExecuteChanged();
            _updateCommand.RaiseCanExecuteChanged();
            _uninstallCommand.RaiseCanExecuteChanged();
            _openCommand.RaiseCanExecuteChanged();
            _openMissingRuntimeCommand.RaiseCanExecuteChanged();
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(Raise);
            return;
        }

        Raise();
    }

    private void OpenMissingRuntimeUrl()
    {
        if (!HasMissingRuntimeUrl) return;
        try
        {
            Process.Start(new ProcessStartInfo(MissingRuntimeUrl!) { UseShellExecute = true });
        }
        catch
        {
            // best effort: se o navegador padrao nao abrir, o usuario ainda ve a mensagem de erro com a URL.
        }
    }
}
