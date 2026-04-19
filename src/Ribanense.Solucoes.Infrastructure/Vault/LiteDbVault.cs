using System.Text.Json;
using LiteDB;
using Ribanense.Solucoes.PluginSDK.Logging;
using Ribanense.Solucoes.PluginSDK.Vault;
using Json = System.Text.Json.JsonSerializer;

namespace Ribanense.Solucoes.Infrastructure.Vault;

/// <summary>
/// Vault LiteDB genérico: metadata, settings key-value, logs estruturados e
/// trilha de auditoria. Não contém lógica de domínio (catálogo WinGet, UWP, etc.)
/// — apps concretos adicionam coleções próprias em seus projetos.
/// </summary>
public sealed class LiteDbVault : IVault
{
    private const string MetadataCollection = "_metadata";
    private const string SettingsCollection = "settings";
    private const string LogsCollection = "logs";
    private const string AuditCollection = "audit_trail";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly LiteDatabase _db;
    private readonly object _lock = new();

    public LiteDbVault(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Caminho obrigatório.", nameof(path));

        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // Mapper LOCAL: evita mutação concorrente do BsonMapper.Global,
        // que é compartilhado por todo o processo e provoca race conditions
        // em cenários multi-instância (testes paralelos, Launcher + utilitário, etc.).
        var mapper = new BsonMapper();
        ConfigureMapper(mapper);

        _db = new LiteDatabase($"Filename={path};Connection=direct", mapper);
        EnsureSeeded();
    }

    private static void ConfigureMapper(BsonMapper mapper)
    {
        mapper.Entity<VaultMetadata>().Id(x => x.Id);
        mapper.Entity<VaultAuditEntry>().Id(x => x.Id);
        mapper.Entity<SettingEntry>().Id(x => x.Key);
        mapper.Entity<LogDocument>().Id(x => x.Id);
    }

    private void EnsureSeeded()
    {
        var meta = _db.GetCollection<VaultMetadata>(MetadataCollection);
        if (meta.Count() > 0) return;

        meta.Insert(new VaultMetadata
        {
            Id = 1,
            SchemaVersion = 1,
            CreatedAtUtc = DateTime.UtcNow,
            LastModifiedAtUtc = DateTime.UtcNow
        });
        WriteAuditInternal("seed", MetadataCollection, null, "Vault criado com defaults.");
    }

    public VaultMetadata GetMetadata()
    {
        lock (_lock)
        {
            return _db.GetCollection<VaultMetadata>(MetadataCollection).FindById(1)
                ?? new VaultMetadata { CreatedAtUtc = DateTime.UtcNow, LastModifiedAtUtc = DateTime.UtcNow };
        }
    }

    public string? GetSetting(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Chave obrigatória.", nameof(key));
        lock (_lock)
        {
            return _db.GetCollection<SettingEntry>(SettingsCollection).FindById(key)?.Value;
        }
    }

    public T? GetSetting<T>(string key)
    {
        string? raw = GetSetting(key);
        if (raw is null) return default;
        if (typeof(T) == typeof(string)) return (T)(object)raw;
        return Json.Deserialize<T>(raw, JsonOpts);
    }

    public void SetSetting(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Chave obrigatória.", nameof(key));
        if (value is null) throw new ArgumentNullException(nameof(value));

        lock (_lock)
        {
            _db.GetCollection<SettingEntry>(SettingsCollection).Upsert(new SettingEntry
            {
                Key = key,
                Value = value,
                UpdatedAtUtc = DateTime.UtcNow
            });
            TouchMetadataInternal();
            WriteAuditInternal("set", SettingsCollection, key, null);
        }
    }

    public void SetSetting<T>(string key, T value)
    {
        string payload = value switch
        {
            null => throw new ArgumentNullException(nameof(value)),
            string s => s,
            _ => Json.Serialize(value, JsonOpts)
        };
        SetSetting(key, payload);
    }

    public bool RemoveSetting(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Chave obrigatória.", nameof(key));
        lock (_lock)
        {
            bool ok = _db.GetCollection<SettingEntry>(SettingsCollection).Delete(key);
            if (ok)
            {
                TouchMetadataInternal();
                WriteAuditInternal("remove", SettingsCollection, key, null);
            }
            return ok;
        }
    }

    public IReadOnlyDictionary<string, string> GetAllSettings()
    {
        lock (_lock)
        {
            return _db.GetCollection<SettingEntry>(SettingsCollection)
                .FindAll()
                .ToDictionary(x => x.Key, x => x.Value);
        }
    }

    public void WriteLog(JsonLogEntry entry)
    {
        if (entry is null) throw new ArgumentNullException(nameof(entry));
        lock (_lock)
        {
            _db.GetCollection<LogDocument>(LogsCollection).Insert(LogDocument.From(entry));
        }
    }

    public IReadOnlyList<JsonLogEntry> GetRecentLogs(int count = 200)
    {
        if (count <= 0) return Array.Empty<JsonLogEntry>();
        lock (_lock)
        {
            return _db.GetCollection<LogDocument>(LogsCollection)
                .Query()
                .OrderByDescending(x => x.Id)
                .Limit(count)
                .ToList()
                .AsEnumerable()
                .Reverse()
                .Select(d => d.ToEntry())
                .ToList();
        }
    }

    public IReadOnlyList<JsonLogEntry> GetLogsBySession(Guid sessionId)
    {
        lock (_lock)
        {
            return _db.GetCollection<LogDocument>(LogsCollection)
                .Find(x => x.SessionId == sessionId)
                .Select(d => d.ToEntry())
                .ToList();
        }
    }

    public IReadOnlyList<VaultAuditEntry> GetAuditTrail(int count = 100)
    {
        if (count <= 0) return Array.Empty<VaultAuditEntry>();
        lock (_lock)
        {
            return _db.GetCollection<VaultAuditEntry>(AuditCollection)
                .Query()
                .OrderByDescending(x => x.Id)
                .Limit(count)
                .ToList();
        }
    }

    public string ExportAllToJson()
    {
        lock (_lock)
        {
            var export = new
            {
                metadata = GetMetadata(),
                settings = GetAllSettings(),
                logs_recentes = GetRecentLogs(50),
                audit_trail = GetAuditTrail(50)
            };
            return Json.Serialize(export, JsonOpts);
        }
    }

    public void Dispose() => _db.Dispose();

    private void TouchMetadataInternal()
    {
        var col = _db.GetCollection<VaultMetadata>(MetadataCollection);
        var meta = col.FindById(1);
        if (meta is not null)
        {
            meta.LastModifiedAtUtc = DateTime.UtcNow;
            col.Update(meta);
        }
    }

    private void WriteAuditInternal(string operation, string collection, string? entityId, string? detail)
    {
        _db.GetCollection<VaultAuditEntry>(AuditCollection).Insert(new VaultAuditEntry
        {
            TimestampUtc = DateTime.UtcNow,
            Operation = operation,
            Collection = collection,
            EntityId = entityId,
            Detail = detail
        });
    }

    // Documentos internos de armazenamento LiteDB.

    private sealed class SettingEntry
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public DateTime UpdatedAtUtc { get; set; }
    }

    private sealed class LogDocument
    {
        public int Id { get; set; }
        public Guid SessionId { get; set; }
        public string AppVersion { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public int ManagedThreadId { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Exception { get; set; }
        public Dictionary<string, string>? Data { get; set; }

        public JsonLogEntry ToEntry() => new(
            SessionId, AppVersion, ProcessId, ManagedThreadId,
            TimestampUtc, Level, Category, Message, Exception,
            Data);

        public static LogDocument From(JsonLogEntry entry) => new()
        {
            SessionId = entry.SessionId,
            AppVersion = entry.AppVersion,
            ProcessId = entry.ProcessId,
            ManagedThreadId = entry.ManagedThreadId,
            TimestampUtc = entry.TimestampUtc,
            Level = entry.Level,
            Category = entry.Category,
            Message = entry.Message,
            Exception = entry.Exception,
            Data = entry.Data is null ? null : new Dictionary<string, string>(entry.Data)
        };
    }
}
