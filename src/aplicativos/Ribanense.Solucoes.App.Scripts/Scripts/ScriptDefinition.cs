namespace Ribanense.Solucoes.App.Scripts.Scripts;

public sealed class ScriptDefinition : IScript
{
    public ScriptDefinition(string id, string name, string description, string category)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        Category = category ?? throw new ArgumentNullException(nameof(category));
    }

    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public string Category { get; }
}
