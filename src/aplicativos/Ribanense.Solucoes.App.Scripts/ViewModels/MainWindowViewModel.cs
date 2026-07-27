using System.Collections.ObjectModel;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Scripts.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private ScriptListItemViewModel? _selectedScript;

    public MainWindowViewModel(IEnumerable<ScriptListItemViewModel> scripts)
    {
        if (scripts is null) throw new ArgumentNullException(nameof(scripts));
        Scripts = new ObservableCollection<ScriptListItemViewModel>(scripts);
        SelectedScript = Scripts.FirstOrDefault();
    }

    public ObservableCollection<ScriptListItemViewModel> Scripts { get; }

    public ScriptListItemViewModel? SelectedScript
    {
        get => _selectedScript;
        set => SetProperty(ref _selectedScript, value);
    }

    public string ProductName => "Scripts";
}
