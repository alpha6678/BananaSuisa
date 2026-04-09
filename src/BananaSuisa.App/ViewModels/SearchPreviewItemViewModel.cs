namespace BananaSuisa.App.ViewModels;

public sealed class SearchPreviewItemViewModel
{
    public SearchPreviewItemViewModel(string kind, string title, string detail)
    {
        Kind = kind;
        Title = title;
        Detail = detail;
    }

    public string Kind { get; }

    public string Title { get; }

    public string Detail { get; }
}
