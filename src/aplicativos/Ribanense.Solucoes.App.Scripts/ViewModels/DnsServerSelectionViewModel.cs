using Ribanense.Solucoes.App.Scripts.Scripts.Dns;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Scripts.ViewModels;

public sealed class DnsServerSelectionViewModel : ObservableObject
{
    private bool _isSelected;

    public DnsServerSelectionViewModel(DnsServerCandidate candidate, bool isSelected = true)
    {
        Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
        _isSelected = isSelected;
    }

    public DnsServerCandidate Candidate { get; }

    public string Label => Candidate.Label;
    public string IpAddress => Candidate.IpAddress;
    public bool CanRemove => Candidate.Origin == DnsServerOrigin.Personalizado;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
