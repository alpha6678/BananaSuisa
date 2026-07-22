using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using Ribanense.Solucoes.App.Winget.Services.Search;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Winget.ViewModels;

public sealed class SearchViewModel : ObservableObject
{
    private readonly ISearchEnhancer _search;
    private readonly IPackageRowHost _host;

    public SearchViewModel(ISearchEnhancer search, IAppAliasCatalog catalog, IPackageRowHost host)
    {
        _search = search;
        _host = host;

        SearchCommand = new AsyncRelayCommand(_ => ExecuteSearchAsync(), _ => !IsBusy && !string.IsNullOrWhiteSpace(Query));
        Results.CollectionChanged += OnResultsChanged;
        LoadSuggestedPackages(catalog);
    }

    public ObservableCollection<PackageRowViewModel> SuggestedPackages { get; } = new();
    public ObservableCollection<PackageRowViewModel> Results { get; } = new();

    private string _query = string.Empty;
    public string Query
    {
        get => _query;
        set
        {
            if (!SetProperty(ref _query, value))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(value) && Results.Count > 0)
            {
                Results.Clear();
                StatusMessage = null;
            }

            OnPropertyChanged(nameof(ShowSuggested));
            CommandManager.InvalidateRequerySuggested();
        }
    }

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

    public bool ShowSuggested =>
        Results.Count == 0 && string.IsNullOrWhiteSpace(Query);

    public ICommand SearchCommand { get; }

    private void LoadSuggestedPackages(IAppAliasCatalog catalog)
    {
        foreach (var alias in catalog.Suggested.Take(20))
        {
            SuggestedPackages.Add(new PackageRowViewModel(
                PackageRowKind.SearchResult,
                alias.PublicName ?? alias.Id,
                alias.Id,
                string.Empty,
                _host)
            {
                Source = "winget",
                Category = alias.Category ?? "Sugerido"
            });
        }
    }

    private async Task ExecuteSearchAsync()
    {
        IsBusy = true;
        StatusMessage = "Buscando...";
        Results.Clear();
        try
        {
            var packages = await _search.SearchAsync(Query, CancellationToken.None).ConfigureAwait(true);
            foreach (var p in packages)
            {
                Results.Add(new PackageRowViewModel(PackageRowKind.SearchResult, p.Name, p.Id, p.Version, _host)
                {
                    Source = p.Source
                });
            }
            StatusMessage = $"{Results.Count} resultado(s).";
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

    private void OnResultsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnPropertyChanged(nameof(ShowSuggested));
}
