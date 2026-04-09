namespace BananaSuisa.App.ViewModels;

public sealed class DiagnosticCheckViewModel
{
    public DiagnosticCheckViewModel(string name, bool isHealthy, string detail)
    {
        Name = name;
        IsHealthy = isHealthy;
        Detail = detail;
    }

    public string Name { get; }

    public bool IsHealthy { get; }

    public string Detail { get; }

    public string StatusText => IsHealthy ? "OK" : "ATENCAO";
}
