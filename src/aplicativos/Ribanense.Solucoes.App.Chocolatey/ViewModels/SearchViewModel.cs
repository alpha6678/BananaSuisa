using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Ribanense.Solucoes.App.Chocolatey.Domain;
using Ribanense.Solucoes.App.Chocolatey.Services;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Chocolatey.ViewModels;

public sealed class SearchViewModel : ObservableObject
{
    private readonly IChocolateySearchService _search;
    private readonly IChocolateyPopularPackagesService _popular;
    private readonly IPackageRowHost _host;

    private bool _popularLoaded;

    public SearchViewModel(
        IChocolateySearchService search,
        IChocolateyPopularPackagesService popular,
        IPackageRowHost host)
    {
        _search = search;
        _popular = popular;
        _host = host;

        SearchCommand = new AsyncRelayCommand(_ => ExecuteSearchAsync(), _ => !IsBusy && !string.IsNullOrWhiteSpace(Query));
    }

    public ObservableCollection<PackageRowViewModel> PopularSuggestions { get; } = new();

    public ObservableCollection<PackageRowViewModel> Results { get; } = new();

    private string _query = string.Empty;
    public string Query
    {
        get => _query;
        set
        {
            if (SetProperty(ref _query, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    private bool _isPopularBusy;
    public bool IsPopularBusy
    {
        get => _isPopularBusy;
        set => SetProperty(ref _isPopularBusy, value);
    }

    private string? _statusMessage;
    public string? StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private string? _popularStatusMessage;
    public string? PopularStatusMessage
    {
        get => _popularStatusMessage;
        set => SetProperty(ref _popularStatusMessage, value);
    }

    public ICommand SearchCommand { get; }

    /// <summary>Carrega os pacotes mais baixados no CCR (OData). Chamado ao abrir a aba Buscar.</summary>
    public async Task EnsurePopularSuggestionsAsync(CancellationToken ct = default)
    {
        if (_popularLoaded) return;

        IsPopularBusy = true;
        PopularStatusMessage = "Carregando mais baixados no repositório da comunidade...";
        PopularSuggestions.Clear();

        try
        {
            IReadOnlyList<ChocolateyGalleryEntry> entries =
                await _popular.GetMostDownloadedDistinctAsync(take: 25, ct).ConfigureAwait(true);

            if (entries.Count == 0)
            {
                entries = OfflineItSuggestions;
                PopularStatusMessage =
                    "Sem dados ao vivo do Chocolatey Community (rede indisponível?). Sugestões offline para TI.";
            }
            else
            {
                PopularStatusMessage =
                    "Ordenado por downloads no Chocolatey Community (feed OData). Instale com um clique.";
            }

            foreach (ChocolateyGalleryEntry e in entries)
            {
                PopularSuggestions.Add(CreatePopularRow(e));
            }

            _popularLoaded = true;
        }
        catch (Exception ex)
        {
            PopularStatusMessage = $"Não foi possível consultar o ranking ao vivo: {ex.Message}. Sugestões offline.";
            foreach (ChocolateyGalleryEntry e in OfflineItSuggestions)
            {
                PopularSuggestions.Add(CreatePopularRow(e));
            }

            _popularLoaded = true;
        }
        finally
        {
            IsPopularBusy = false;
        }
    }

    private PackageRowViewModel CreatePopularRow(ChocolateyGalleryEntry e)
    {
        string? dl = e.DownloadCount > 0 ? FormatDownloadCount(e.DownloadCount) : null;
        return new PackageRowViewModel(PackageRowKind.SearchResult, e.Id, e.Id, e.Version, _host)
        {
            Source = "Chocolatey Community",
            DownloadSummary = dl
        };
    }

    private static string FormatDownloadCount(long count)
    {
        if (count >= 1_000_000)
        {
            double m = count / 1_000_000.0;
            return $"~{m.ToString("0.#", CultureInfo.GetCultureInfo("pt-BR"))}M downloads (CCR)";
        }

        if (count >= 1_000)
        {
            double k = count / 1_000.0;
            return $"~{k.ToString("0.#", CultureInfo.GetCultureInfo("pt-BR"))}k downloads (CCR)";
        }

        return count.ToString("N0", CultureInfo.GetCultureInfo("pt-BR")) + " downloads (CCR)";
    }

    /// <summary>Sugestões estáticas comuns em ambientes de TI se o feed OData não estiver acessível.</summary>
    private static readonly ChocolateyGalleryEntry[] OfflineItSuggestions =
    {
        new("git", "—", 0),
        new("vscode", "—", 0),
        new("7zip", "—", 0),
        new("notepadplusplus", "—", 0),
        new("putty", "—", 0),
        new("wireshark", "—", 0),
        new("sysinternals", "—", 0),
        new("googlechrome", "—", 0),
        new("firefox", "—", 0),
        new("winscp", "—", 0)
    };

    private async Task ExecuteSearchAsync()
    {
        IsBusy = true;
        StatusMessage = "Buscando no Chocolatey...";
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
}
