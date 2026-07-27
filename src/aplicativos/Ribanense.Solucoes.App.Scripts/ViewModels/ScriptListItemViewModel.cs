using Ribanense.Solucoes.App.Scripts.Scripts;

namespace Ribanense.Solucoes.App.Scripts.ViewModels;

/// <summary>
/// Envolve os metadados de um <see cref="IScript"/> junto com o ViewModel de
/// conteúdo exibido quando o usuário o seleciona na lista (mapeado para uma
/// View via DataTemplate em App.xaml).
/// </summary>
public sealed class ScriptListItemViewModel
{
    public ScriptListItemViewModel(IScript script, object content)
    {
        Script = script ?? throw new ArgumentNullException(nameof(script));
        Content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public IScript Script { get; }
    public object Content { get; }

    public string Name => Script.Name;
    public string Description => Script.Description;
    public string Category => Script.Category;
}
