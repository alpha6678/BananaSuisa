using System.Text.Json;
using Ribanense.Solucoes.PluginSDK.Logging;
using Ribanense.Solucoes.PluginSDK.Vault;

namespace Ribanense.Solucoes.Launcher.Tests.Helpers;

public sealed class InMemoryVault : IVault
{
    private readonly Dictionary<string, string> _settings = new(StringComparer.Ordinal);
    private readonly List<JsonLogEntry> _logs = new();
    private readonly List<VaultAuditEntry> _audit = new();
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public VaultMetadata Metadata { get; } = new()
    {
        Id = 1,
        SchemaVersion = 1,
        CreatedAtUtc = DateTime.UtcNow,
        LastModifiedAtUtc = DateTime.UtcNow
    };

    public VaultMetadata GetMetadata() => Metadata;

    public string? GetSetting(string key) => _settings.TryGetValue(key, out var v) ? v : null;

    public T? GetSetting<T>(string key)
    {
        string? raw = GetSetting(key);
        if (raw is null) return default;
        if (typeof(T) == typeof(string)) return (T)(object)raw;
        return JsonSerializer.Deserialize<T>(raw, JsonOpts);
    }

    public void SetSetting(string key, string value) => _settings[key] = value;

    public void SetSetting<T>(string key, T value)
    {
        string payload = value switch
        {
            null => throw new ArgumentNullException(nameof(value)),
            string s => s,
            _ => JsonSerializer.Serialize(value, JsonOpts)
        };
        SetSetting(key, payload);
    }

    public bool RemoveSetting(string key) => _settings.Remove(key);

    public IReadOnlyDictionary<string, string> GetAllSettings() =>
        new Dictionary<string, string>(_settings);

    public void WriteLog(JsonLogEntry entry) => _logs.Add(entry);

    public IReadOnlyList<JsonLogEntry> GetRecentLogs(int count = 200) =>
        _logs.TakeLast(count).ToList();

    public IReadOnlyList<JsonLogEntry> GetLogsBySession(Guid sessionId) =>
        _logs.Where(l => l.SessionId == sessionId).ToList();

    public IReadOnlyList<VaultAuditEntry> GetAuditTrail(int count = 100) =>
        _audit.TakeLast(count).ToList();

    public string ExportAllToJson() => JsonSerializer.Serialize(new { _settings, _logs, _audit }, JsonOpts);

    public void Dispose() { }
}
