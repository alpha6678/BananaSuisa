namespace BananaSuisa.App.ViewModels;

public sealed class BootstrapPathRowViewModel
{
    public BootstrapPathRowViewModel(string key, string value)
    {
        Key = key;
        Value = value;
    }

    public string Key { get; }

    public string Value { get; }
}
