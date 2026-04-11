using BananaSuisa.Core.Catalog;
using BananaSuisa.Core.Logging;
using BananaSuisa.Core.Vault;

namespace BananaSuisa.Services.Abstractions;

public interface IVault : IDisposable
{
    VaultMetadata GetMetadata();

    VaultSettings GetSettings();
    void SaveSettings(VaultSettings settings);

    IReadOnlyList<CatalogItem> GetCatalogItems();
    void UpsertCatalogItem(CatalogItem item);
    void DeleteCatalogItem(string packageId);
    int ImportCatalogFromJson(string json);
    string ExportCatalogToJson();

    void WriteLog(JsonLogEntry entry);
    IReadOnlyList<JsonLogEntry> GetRecentLogs(int count = 200);
    IReadOnlyList<JsonLogEntry> GetLogsBySession(Guid sessionId);

    IReadOnlyList<VaultAuditEntry> GetAuditTrail(int count = 100);

    string ExportAllToJson();
}
