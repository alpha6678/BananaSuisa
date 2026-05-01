using System.Windows.Input;
using Ribanense.Solucoes.App.Chocolatey.Services.Sources;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Chocolatey.ViewModels;

public interface ISourceRowHost
{
    Task RemoveAsync(SourceRowViewModel row);
}

public sealed class SourceRowViewModel : ObservableObject
{
    public SourceRowViewModel(ChocolateySource source, ISourceRowHost host)
    {
        Source = source;
        RemoveCommand = new AsyncRelayCommand(_ => host.RemoveAsync(this), _ => !IsBusy);
    }

    public ChocolateySource Source { get; }

    public string Name => Source.Name;
    public string Url => Source.Url;
    public string Disabled => Source.Disabled ? "Sim" : "Não";
    public string Priority => Source.Priority;

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    private string? _status;
    public string? Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public ICommand RemoveCommand { get; }
}
