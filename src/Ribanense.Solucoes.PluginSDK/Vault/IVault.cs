using Ribanense.Solucoes.PluginSDK.Logging;

namespace Ribanense.Solucoes.PluginSDK.Vault;

/// <summary>
/// Vault persistente genérico usado por qualquer app do ecossistema Ribanense.
/// Guarda metadata, settings key-value, logs estruturados e trilha de auditoria.
/// Implementações devem ser thread-safe.
/// </summary>
public interface IVault : IDisposable
{
    VaultMetadata GetMetadata();

    string? GetSetting(string key);
    T? GetSetting<T>(string key);
    void SetSetting(string key, string value);
    void SetSetting<T>(string key, T value);
    bool RemoveSetting(string key);
    IReadOnlyDictionary<string, string> GetAllSettings();

    void WriteLog(JsonLogEntry entry);
    IReadOnlyList<JsonLogEntry> GetRecentLogs(int count = 200);
    IReadOnlyList<JsonLogEntry> GetLogsBySession(Guid sessionId);

    IReadOnlyList<VaultAuditEntry> GetAuditTrail(int count = 100);

    string ExportAllToJson();
}
