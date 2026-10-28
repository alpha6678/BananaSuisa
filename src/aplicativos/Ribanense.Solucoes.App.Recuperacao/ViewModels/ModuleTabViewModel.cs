using Ribanense.Solucoes.UI.Mvvm;

namespace Ribanense.Solucoes.App.Recuperacao.ViewModels;

/// <summary>Aba de módulo (hoje: Áudio; novos módulos de recuperação entram aqui).</summary>
public sealed class ModuleTabViewModel : ObservableObject
{
    public ModuleTabViewModel(string title, object content)
    {
        Title = title;
        Content = content;
    }

    public string Title { get; }

    /// <summary>ViewModel exibido no ContentControl (roteado por DataTemplate).</summary>
    public object Content { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
