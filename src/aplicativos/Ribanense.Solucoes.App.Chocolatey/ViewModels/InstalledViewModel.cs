using System.Collections.ObjectModel;
using System.Windows.Input;
using Ribanense.Solucoes.App.Chocolatey.Services;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Chocolatey.ViewModels;

public sealed class InstalledViewModel : ObservableObject
{
    private readonly IChocolateyListService _list;
    private readonly IPackageRowHost _host;

    public InstalledViewModel(IChocolateyListService list, IPackageRowHost host)
    {
        _list = list;
        _host = host;
        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync(), _ => !IsBusy);
    }

    public ObservableCollection<PackageRowViewModel> Packages { get; } = new();

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    private string? _statusMessage;
    public string? StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand RefreshCommand { get; }

    public async Task RefreshAsync()
    {
        IsBusy = true;
        StatusMessage = "Listando pacotes instalados...";
        Packages.Clear();
        try
        {
            var installed = await _list.GetInstalledAsync(CancellationToken.None).ConfigureAwait(true);
            foreach (var p in installed)
            {
                Packages.Add(new PackageRowViewModel(PackageRowKind.Installed, p.Name, p.Id, p.InstalledVersion, _host)
                {
                    AvailableVersion = p.AvailableVersion,
                    Source = p.Source
                });
            }
            int withUpdate = Packages.Count(x => x.HasUpdate);
            StatusMessage = withUpdate > 0
                ? $"{Packages.Count} instalado(s), {withUpdate} com atualização."
                : $"{Packages.Count} instalado(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
