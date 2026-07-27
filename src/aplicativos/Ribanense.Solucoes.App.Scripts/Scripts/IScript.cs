namespace Ribanense.Solucoes.App.Scripts.Scripts;

/// <summary>
/// Metadados de um script exibido na lista principal do app Scripts.
/// O conteúdo interativo de cada script fica em seu próprio ViewModel
/// (ver <see cref="Ribanense.Solucoes.App.Scripts.ViewModels.ScriptListItemViewModel"/>),
/// exibido via DataTemplate quando o usuário o seleciona.
/// </summary>
public interface IScript
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    string Category { get; }
}
