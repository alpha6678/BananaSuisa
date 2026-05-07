using System.Text.Json.Serialization;

namespace Ribanense.Solucoes.App.Winget.Services.Search;

public sealed class AppAlias
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("publisher")] public string? Publisher { get; init; }
    [JsonPropertyName("publicName")] public string? PublicName { get; init; }
    [JsonPropertyName("synonyms")] public List<string> Synonyms { get; init; } = new();
    [JsonPropertyName("category")] public string? Category { get; init; }
    [JsonPropertyName("isSuggested")] public bool IsSuggested { get; init; }
    [JsonPropertyName("suggestedOrder")] public int? SuggestedOrder { get; init; }
    [JsonPropertyName("auditStatus")] public string? AuditStatus { get; init; }
}
