using System.Windows.Input;
using Ribanense.Solucoes.App.Winget.Services.Sources;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Winget.ViewModels;

public interface ISourceRowHost
{
    Task UpdateAsync(SourceRowViewModel row);
    Task RemoveAsync(SourceRowViewModel row);
}

public sealed class SourceRowViewModel : ObservableObject
{
    private readonly ISourceRowHost _host;

    public SourceRowViewModel(WingetSource source, ISourceRowHost host)
    {
        Source = source;
        _host = host;

        UpdateCommand = new AsyncRelayCommand(_ => _host.UpdateAsync(this), _ => !IsBusy);
        RemoveCommand = new AsyncRelayCommand(_ => _host.RemoveAsync(this), _ => !IsBusy);
    }

    public WingetSource Source { get; }

    public string Name => Source.Name;
    public string Argument => Source.Argument;
    public string Type => Source.Type;
    public string TrustLevel => Source.TrustLevel ?? string.Empty;

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

    public ICommand UpdateCommand { get; }
    public ICommand RemoveCommand { get; }
}
