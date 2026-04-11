namespace BananaSuisa.App.ViewModels;

/// <summary>
/// Linha da pesquisa de catálogo com caixa de seleção para fila de instalação.
/// </summary>
public sealed class WingetCatalogPickRowViewModel : ObservableObject
{
    private readonly MainWindowViewModel _owner;
    private bool _isSelected;

    public WingetCatalogPickRowViewModel(
        MainWindowViewModel owner,
        string name,
        string id,
        string version,
        string source,
        string installationOrigin,
        bool initialSelected)
    {
        _owner = owner;
        Name = name;
        Id = id;
        Version = version;
        Source = source;
        InstallationOrigin = installationOrigin;
        _isSelected = initialSelected;
    }

    public string Name { get; }

    public string Id { get; }

    public string Version { get; }

    public string Source { get; }

    public string InstallationOrigin { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                _owner.OnCatalogPickSelectionChanged(this);
            }
        }
    }

    /// <summary>Atualiza o estado sem notificar o dono (ex.: após sincronizar com lista instalada).
    /// </summary>
    public void SetSelectedSilent(bool value)
    {
        if (_isSelected == value)
        {
            return;
        }

        _isSelected = value;
        OnPropertyChanged(nameof(IsSelected));
    }
}
