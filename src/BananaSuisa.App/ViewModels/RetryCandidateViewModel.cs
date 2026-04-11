namespace BananaSuisa.App.ViewModels;

/// <summary>
/// Candidato de retry por similaridade: associa um item falhado a uma alternativa encontrada no repositório.
/// </summary>
public sealed class RetryCandidateViewModel : ObservableObject
{
    private bool _isApproved = true;
    private string _retryStatus = string.Empty;

    public RetryCandidateViewModel(
        string originalName,
        string originalId,
        string suggestedName,
        string suggestedId,
        string suggestedSource,
        int relevanceScore)
    {
        OriginalName = originalName;
        OriginalId = originalId;
        SuggestedName = suggestedName;
        SuggestedId = suggestedId;
        SuggestedSource = suggestedSource;
        RelevanceScore = relevanceScore;
        HasSuggestion = !string.IsNullOrEmpty(suggestedId);
    }

    public string OriginalName { get; }
    public string OriginalId { get; }
    public string SuggestedName { get; }
    public string SuggestedId { get; }
    public string SuggestedSource { get; }
    public int RelevanceScore { get; }
    public bool HasSuggestion { get; }

    public bool IsApproved
    {
        get => _isApproved;
        set => SetProperty(ref _isApproved, value);
    }

    public string RetryStatus
    {
        get => _retryStatus;
        set => SetProperty(ref _retryStatus, value);
    }
}
