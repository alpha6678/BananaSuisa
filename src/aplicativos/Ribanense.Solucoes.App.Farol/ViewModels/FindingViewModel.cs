using Ribanense.Solucoes.App.Farol.Domain;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Farol.ViewModels;

public sealed class FindingViewModel : ObservableObject
{
    private bool _isExpanded;

    public FindingViewModel(Finding finding)
    {
        Finding = finding ?? throw new ArgumentNullException(nameof(finding));
    }

    public Finding Finding { get; }

    public string Title => Finding.Title;
    public string Category => Finding.Category;
    public string Evidence => Finding.Evidence;
    public string Suggestion => Finding.Suggestion;
    public FindingSeverity Severity => Finding.Severity;

    public string SeverityLabel => Finding.Severity switch
    {
        FindingSeverity.High => "Grave",
        FindingSeverity.Medium => "Atenção",
        FindingSeverity.Low => "Baixo",
        _ => "Info",
    };

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }
}
