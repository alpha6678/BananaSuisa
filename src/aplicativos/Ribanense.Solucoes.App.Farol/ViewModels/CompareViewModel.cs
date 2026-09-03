using System.Collections.ObjectModel;
using Ribanense.Solucoes.App.Farol.Analysis;
using Ribanense.Solucoes.App.Farol.Domain;
using Ribanense.Solucoes.App.Farol.Services;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Farol.ViewModels;

/// <summary>Diff desta máquina contra um farol irmão da mesma rede.</summary>
public sealed class CompareViewModel : ObservableObject
{
    private readonly FarolStation _station;
    private readonly BundleComparer _comparer;

    private PeerCardViewModel? _selectedPeer;
    private bool _isComparing;
    private string? _statusMessage;

    public CompareViewModel(FarolStation station, MapViewModel map, BundleComparer comparer)
    {
        _station = station ?? throw new ArgumentNullException(nameof(station));
        _comparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
        Peers = (map ?? throw new ArgumentNullException(nameof(map))).Peers;

        CompareCommand = new AsyncRelayCommand(CompareAsync, () => SelectedPeer is not null && !IsComparing);
    }

    public ObservableCollection<PeerCardViewModel> Peers { get; }
    public ObservableCollection<BundleDifference> Differences { get; } = new();

    public AsyncRelayCommand CompareCommand { get; }

    public PeerCardViewModel? SelectedPeer
    {
        get => _selectedPeer;
        set => SetProperty(ref _selectedPeer, value);
    }

    public bool IsComparing
    {
        get => _isComparing;
        private set => SetProperty(ref _isComparing, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool HasDifferences => Differences.Count > 0;

    private async Task CompareAsync()
    {
        if (SelectedPeer is not { } target) return;

        if (_station.LatestBundle is not { } local)
        {
            StatusMessage = "Capture o dossiê desta máquina antes de comparar.";
            return;
        }

        IsComparing = true;
        Differences.Clear();
        StatusMessage = $"Buscando o dossiê de {target.DisplayName}…";

        try
        {
            EvidenceBundle? remote = await _station.Client
                .GetLatestBundleAsync(target.Peer, CancellationToken.None)
                .ConfigureAwait(true);

            if (remote is null)
            {
                StatusMessage = $"{target.DisplayName} não devolveu dossiê. Peça uma captura naquela máquina.";
                return;
            }

            foreach (BundleDifference difference in _comparer.Compare(local, remote))
            {
                Differences.Add(difference);
            }

            StatusMessage = Differences.Count == 0
                ? $"Nenhuma diferença relevante em relação a {target.DisplayName}."
                : $"{Differences.Count} diferença(s) em relação a {target.DisplayName}.";
        }
        finally
        {
            IsComparing = false;
            OnPropertyChanged(nameof(HasDifferences));
        }
    }
}
