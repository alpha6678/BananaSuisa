namespace BananaSuisa.Core.Configuration;

public sealed class BananaSuisaProfile
{
    public string Description { get; init; } = string.Empty;

    public string Color { get; init; } = string.Empty;

    public IReadOnlyList<string> Apps { get; init; } = [];
}
