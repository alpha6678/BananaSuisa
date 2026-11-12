using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Ribanense.Solucoes.Launcher.ViewModels;

public sealed class MyAppsViewModel : PageViewModel
{
    private readonly CatalogViewModel _source;

    public MyAppsViewModel(CatalogViewModel source)
    {
        _source = source;

        _source.Cards.CollectionChanged += (_, args) =>
        {
            if (args.NewItems is not null)
            {
                foreach (AppCardViewModel card in args.NewItems)
                    card.PropertyChanged += OnCardChanged;
            }

            if (args.OldItems is not null)
            {
                foreach (AppCardViewModel card in args.OldItems)
                    card.PropertyChanged -= OnCardChanged;
            }

            Refresh();
        };

        Refresh();
    }

    public override string Title => "Meus apps";
    public override string Icon => "📦";

    public ObservableCollection<AppCardViewModel> Cards { get; } = new();

    public override Task OnActivatedAsync()
    {
        Refresh();
        return Task.CompletedTask;
    }

    /// <summary>Instalar, atualizar ou desinstalar muda a lista sem recriar os cartões.</summary>
    private void OnCardChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppCardViewModel.Installed))
            Refresh();
    }

    private void Refresh()
    {
        var installed = _source.Cards.Where(c => c.Installed is not null).ToList();

        if (installed.Count != Cards.Count || !installed.SequenceEqual(Cards))
        {
            Cards.Clear();
            foreach (var card in installed) Cards.Add(card);
        }

        StatusMessage = Cards.Count == 0
            ? "Nenhum app instalado ainda. Abra o Catálogo para instalar."
            : Cards.Count == 1 ? "1 app instalado." : $"{Cards.Count} apps instalados.";
    }
}
