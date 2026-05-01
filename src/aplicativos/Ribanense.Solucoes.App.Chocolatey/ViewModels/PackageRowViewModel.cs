using System.Windows.Input;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Chocolatey.ViewModels;

public enum PackageRowKind
{
    SearchResult,
    Installed
}

public interface IPackageRowHost
{
    Task InstallAsync(PackageRowViewModel row);
    Task UninstallAsync(PackageRowViewModel row);
    Task UpgradeAsync(PackageRowViewModel row);
}

public sealed class PackageRowViewModel : ObservableObject
{
    public PackageRowViewModel(PackageRowKind kind, string name, string id, string version, IPackageRowHost host)
    {
        Kind = kind;
        Name = name;
        Id = id;
        Version = version;

        InstallCommand = new AsyncRelayCommand(_ => host.InstallAsync(this), _ => CanInstall);
        UninstallCommand = new AsyncRelayCommand(_ => host.UninstallAsync(this), _ => CanUninstall);
        UpgradeCommand = new AsyncRelayCommand(_ => host.UpgradeAsync(this), _ => CanUpgrade);
    }

    public PackageRowKind Kind { get; }
    public string Name { get; }
    public string Id { get; }
    public string Version { get; }

    private string? _availableVersion;
    public string? AvailableVersion
    {
        get => _availableVersion;
        set
        {
            if (SetProperty(ref _availableVersion, value))
            {
                OnPropertyChanged(nameof(CanUpgrade));
                OnPropertyChanged(nameof(HasUpdate));
            }
        }
    }

    public string Source { get; init; } = string.Empty;

    /// <summary>Texto opcional (ex.: downloads no CCR) para colunas de descoberta.</summary>
    public string? DownloadSummary { get; init; }

    public bool HasUpdate =>
        !string.IsNullOrWhiteSpace(AvailableVersion)
        && !string.Equals(AvailableVersion, Version, StringComparison.OrdinalIgnoreCase);

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanInstall));
                OnPropertyChanged(nameof(CanUninstall));
                OnPropertyChanged(nameof(CanUpgrade));
            }
        }
    }

    private string? _status;
    public string? Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public bool CanInstall => Kind == PackageRowKind.SearchResult && !IsBusy;
    public bool CanUninstall => Kind == PackageRowKind.Installed && !IsBusy;
    public bool CanUpgrade => Kind == PackageRowKind.Installed && !IsBusy && HasUpdate;

    public ICommand InstallCommand { get; }
    public ICommand UninstallCommand { get; }
    public ICommand UpgradeCommand { get; }
}
