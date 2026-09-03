using System.Collections.ObjectModel;
using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Farol.ViewModels;

public sealed class SectionViewModel
{
    public SectionViewModel(string name, string description, object content)
    {
        Name = name;
        Description = description;
        Content = content;
    }

    public string Name { get; }
    public string Description { get; }
    public object Content { get; }
}

public sealed class MainWindowViewModel : ObservableObject
{
    private SectionViewModel? _selectedSection;

    public MainWindowViewModel(IEnumerable<SectionViewModel> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);

        Sections = new ObservableCollection<SectionViewModel>(sections);
        SelectedSection = Sections.FirstOrDefault();
    }

    public ObservableCollection<SectionViewModel> Sections { get; }

    public SectionViewModel? SelectedSection
    {
        get => _selectedSection;
        set => SetProperty(ref _selectedSection, value);
    }

    public string ProductName => "Farol";

    public void Select(string sectionName)
    {
        SectionViewModel? target = Sections.FirstOrDefault(s =>
            string.Equals(s.Name, sectionName, StringComparison.OrdinalIgnoreCase));

        if (target is not null) SelectedSection = target;
    }
}
