namespace BananaSuisa.App.ViewModels;

public sealed class WingetSearchRowViewModel
{
    public WingetSearchRowViewModel(string name, string id, string version, string source)
    {
        Name = name;
        Id = id;
        Version = version;
        Source = source;
    }

    public string Name { get; }

    public string Id { get; }

    public string Version { get; }

    public string Source { get; }
}
