using System.Collections.ObjectModel;
using System.Windows.Input;
using Ribanense.Solucoes.Launcher.Domain;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.Launcher.ViewModels;

public sealed class UpdatesViewModel : PageViewModel
{
    private readonly CatalogViewModel _source;
    private readonly IAppCardHost _host;

    public UpdatesViewModel(CatalogViewModel source, IAppCardHost host)
    {
        _source = source;
        _host = host;
        _source.Cards.CollectionChanged += (_, _) => Refresh();
        foreach (var card in _source.Cards)
        {
            card.PropertyChanged += (_, _) => Refresh();
        }
        _source.Cards.CollectionChanged += (_, args) =>
        {
            if (args.NewItems is null) return;
            foreach (AppCardViewModel c in args.NewItems)
            {
                c.PropertyChanged += (_, _) => Refresh();
            }
        };

        UpdateAllCommand = new AsyncRelayCommand(_ => UpdateAllAsync(), _ => Cards.Count > 0 && !IsBusy);
        Refresh();
    }

    public override string Title => "Atualizações";
    public override string Icon => "⬆";

    public ObservableCollection<AppCardViewModel> Cards { get; } = new();

    public ICommand UpdateAllCommand { get; }

    public int Count => Cards.Count;

    public override Task OnActivatedAsync()
    {
        Refresh();
        return Task.CompletedTask;
    }

    private void Refresh()
    {
        Cards.Clear();
        foreach (var c in _source.Cards)
        {
            if (c.Status == UpdateStatus.UpdateAvailable) Cards.Add(c);
        }
        OnPropertyChanged(nameof(Count));
        StatusMessage = Cards.Count == 0 ? "Tudo atualizado." : $"{Cards.Count} atualização(ões) disponível(is).";
    }

    private async Task UpdateAllAsync()
    {
        IsBusy = true;
        try
        {
            foreach (var card in Cards.ToList())
            {
                await _host.UpdateAsync(card);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
