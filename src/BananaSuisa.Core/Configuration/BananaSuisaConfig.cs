using System.Text.Json;

namespace BananaSuisa.Core.Configuration;

public sealed class BananaSuisaConfig
{
    public string Version { get; init; } = string.Empty;

    public IReadOnlyList<string> Apps { get; init; } = [];

    public IReadOnlyDictionary<string, BananaSuisaProfile> Profiles { get; init; } = new Dictionary<string, BananaSuisaProfile>();

    public string DefaultProfile { get; init; } = string.Empty;

    public IReadOnlyList<JsonElement> CustomApps { get; init; } = [];

    public BananaSuisaSettings Settings { get; init; } = new();
}
