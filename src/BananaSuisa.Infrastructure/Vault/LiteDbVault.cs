using BananaSuisa.Core.Catalog;
using BananaSuisa.Core.Logging;
using BananaSuisa.Core.Vault;
using BananaSuisa.Services.Abstractions;
using LiteDB;
using Json = System.Text.Json.JsonSerializer;
using JsonOpts_t = System.Text.Json.JsonSerializerOptions;
using JsonNaming = System.Text.Json.JsonNamingPolicy;

namespace BananaSuisa.Infrastructure.Vault;

public sealed class LiteDbVault : IVault
{
    private readonly LiteDatabase _db;
    private readonly object _lock = new();
    private static readonly JsonOpts_t JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNaming.CamelCase
    };

    public LiteDbVault(string path)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _db = new LiteDatabase($"Filename={path};Connection=direct");
        ConfigureMapper();
        EnsureSeeded();
    }

    private void ConfigureMapper()
    {
        var mapper = BsonMapper.Global;

        mapper.Entity<CatalogDocument>().Id(x => x.PackageId);
        mapper.Entity<LogDocument>().Id(x => x.Id);
        mapper.Entity<VaultMetadata>().Id(x => x.Id);
        mapper.Entity<VaultSettings>().Id(x => x.Id);
        mapper.Entity<VaultAuditEntry>().Id(x => x.Id);
    }

    private void EnsureSeeded()
    {
        var meta = _db.GetCollection<VaultMetadata>("_metadata");
        if (meta.Count() > 0)
            return;

        meta.Insert(new VaultMetadata
        {
            SchemaVersion = 1,
            CreatedAtUtc = DateTime.UtcNow,
            LastModifiedAtUtc = DateTime.UtcNow
        });

        _db.GetCollection<VaultSettings>("settings")
            .Insert(new VaultSettings());

        var catalog = _db.GetCollection<CatalogDocument>("catalogo");
        foreach (var item in ItProfessionalsCatalog.GetRecommendations())
        {
            catalog.Insert(CatalogDocument.From(item));
        }

        WriteAudit("seed", "_metadata", null, "Vault criado com defaults e catalogo curado.");
    }

    // -- Metadata --

    public VaultMetadata GetMetadata()
    {
        lock (_lock)
            return _db.GetCollection<VaultMetadata>("_metadata").FindById(1)
                   ?? new VaultMetadata { CreatedAtUtc = DateTime.UtcNow, LastModifiedAtUtc = DateTime.UtcNow };
    }

    // -- Settings --

    public VaultSettings GetSettings()
    {
        lock (_lock)
            return _db.GetCollection<VaultSettings>("settings").FindById(1) ?? new VaultSettings();
    }

    public void SaveSettings(VaultSettings settings)
    {
        lock (_lock)
        {
            settings.Id = 1;
            _db.GetCollection<VaultSettings>("settings").Upsert(settings);
            TouchMetadata();
            WriteAudit("update", "settings", null, "Settings atualizados.");
        }
    }

    // -- Catalogo --

    public IReadOnlyList<CatalogItem> GetCatalogItems()
    {
        lock (_lock)
            return _db.GetCollection<CatalogDocument>("catalogo")
                .FindAll()
                .Select(d => d.ToCatalogItem())
                .ToList();
    }

    public void UpsertCatalogItem(CatalogItem item)
    {
        lock (_lock)
        {
            _db.GetCollection<CatalogDocument>("catalogo").Upsert(CatalogDocument.From(item));
            TouchMetadata();
            WriteAudit("upsert", "catalogo", item.PackageId, item.Name);
        }
    }

    public void DeleteCatalogItem(string packageId)
    {
        lock (_lock)
        {
            _db.GetCollection<CatalogDocument>("catalogo").Delete(packageId);
            TouchMetadata();
            WriteAudit("delete", "catalogo", packageId, null);
        }
    }

    public int ImportCatalogFromJson(string json)
    {
        var items = Json.Deserialize<List<CatalogJsonImport>>(json, JsonOptions) ?? [];
        int count = 0;
        lock (_lock)
        {
            var col = _db.GetCollection<CatalogDocument>("catalogo");
            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.PackageId)) continue;
                col.Upsert(new CatalogDocument
                {
                    PackageId = item.PackageId,
                    Name = item.Name ?? item.PackageId,
                    Category = item.Category ?? "Importado",
                    IsEssential = item.IsEssential,
                    SourceName = item.SourceName ?? "Import"
                });
                count++;
            }

            TouchMetadata();
            WriteAudit("import", "catalogo", null, $"Importados {count} itens de JSON.");
        }

        return count;
    }

    public string ExportCatalogToJson()
    {
        var items = GetCatalogItems();
        return Json.Serialize(items, JsonOptions);
    }

    // -- Logs --

    public void WriteLog(JsonLogEntry entry)
    {
        lock (_lock)
            _db.GetCollection<LogDocument>("logs").Insert(LogDocument.From(entry));
    }

    public IReadOnlyList<JsonLogEntry> GetRecentLogs(int count = 200)
    {
        lock (_lock)
            return _db.GetCollection<LogDocument>("logs")
                .Query()
                .OrderByDescending(x => x.Id)
                .Limit(count)
                .ToList()
                .Select(d => d.ToEntry())
                .Reverse()
                .ToList();
    }

    public IReadOnlyList<JsonLogEntry> GetLogsBySession(Guid sessionId)
    {
        lock (_lock)
            return _db.GetCollection<LogDocument>("logs")
                .Find(x => x.SessionId == sessionId)
                .Select(d => d.ToEntry())
                .ToList();
    }

    // -- Audit --

    public IReadOnlyList<VaultAuditEntry> GetAuditTrail(int count = 100)
    {
        lock (_lock)
            return _db.GetCollection<VaultAuditEntry>("audit_trail")
                .Query()
                .OrderByDescending(x => x.Id)
                .Limit(count)
                .ToList()
                .AsReadOnly();
    }

    // -- Export --

    public string ExportAllToJson()
    {
        lock (_lock)
        {
            var export = new
            {
                metadata = GetMetadata(),
                settings = GetSettings(),
                catalogo = GetCatalogItems(),
                logs_recentes = GetRecentLogs(50),
                audit_trail = GetAuditTrail(50)
            };

            return Json.Serialize(export, JsonOptions);
        }
    }

    // -- Internal --

    private void TouchMetadata()
    {
        var meta = _db.GetCollection<VaultMetadata>("_metadata").FindById(1);
        if (meta is not null)
        {
            meta.LastModifiedAtUtc = DateTime.UtcNow;
            _db.GetCollection<VaultMetadata>("_metadata").Update(meta);
        }
    }

    private void WriteAudit(string operation, string collection, string? entityId, string? detail)
    {
        _db.GetCollection<VaultAuditEntry>("audit_trail").Insert(new VaultAuditEntry
        {
            TimestampUtc = DateTime.UtcNow,
            Operation = operation,
            Collection = collection,
            EntityId = entityId,
            Detail = detail
        });
    }

    public void Dispose() => _db.Dispose();

    // -- Documentos internos para LiteDB --

    private sealed class CatalogDocument
    {
        public string PackageId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool IsEssential { get; set; }
        public string SourceName { get; set; } = string.Empty;

        public CatalogItem ToCatalogItem() => new(Name, PackageId, Category, IsEssential, SourceName);

        public static CatalogDocument From(CatalogItem item) => new()
        {
            PackageId = item.PackageId,
            Name = item.Name,
            Category = item.Category,
            IsEssential = item.IsEssential,
            SourceName = item.SourceName
        };
    }

    private sealed class CatalogJsonImport
    {
        public string? Name { get; set; }
        public string? PackageId { get; set; }
        public string? Category { get; set; }
        public bool IsEssential { get; set; }
        public string? SourceName { get; set; }
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
