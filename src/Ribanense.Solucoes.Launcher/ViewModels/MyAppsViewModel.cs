using System.Collections.ObjectModel;

namespace Ribanense.Solucoes.Launcher.ViewModels;

public sealed class MyAppsViewModel : PageViewModel
{
    public MyAppsViewModel(CatalogViewModel source)
    {
        _source = source;
        _source.Cards.CollectionChanged += (_, _) => Refresh();
        Refresh();
    }

    private readonly CatalogViewModel _source;

    public override string Title => "Meus apps";
    public override string Icon => "📦";

    public ObservableCollection<AppCardViewModel> Cards { get; } = new();

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
            if (c.Installed is not null) Cards.Add(c);
        }
        StatusMessage = Cards.Count == 0 ? "Nenhum app instalado ainda." : null;
    }
}
