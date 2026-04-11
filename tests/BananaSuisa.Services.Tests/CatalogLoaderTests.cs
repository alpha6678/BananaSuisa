using BananaSuisa.Core.Catalog;
using BananaSuisa.Core.Vault;
using BananaSuisa.Infrastructure.Vault;
using BananaSuisa.Services.Abstractions;

namespace BananaSuisa.Services.Tests;

public sealed class VaultTests : IDisposable
{
    private readonly string _vaultPath;
    private readonly IVault _vault;

    public VaultTests()
    {
        _vaultPath = Path.Combine(Path.GetTempPath(), "BananaSuisa.Tests", Guid.NewGuid().ToString("N"), "test.dat");
        _vault = new LiteDbVault(_vaultPath);
    }

    [Fact]
    public void NewVault_IsSeededWithMetadataAndCatalog()
    {
        VaultMetadata meta = _vault.GetMetadata();

        Assert.Equal(1, meta.SchemaVersion);
        Assert.True(meta.CreatedAtUtc > DateTime.MinValue);

        IReadOnlyList<CatalogItem> items = _vault.GetCatalogItems();
        Assert.True(items.Count > 100);
    }

    [Fact]
    public void GetSettings_ReturnsDefaultsForNewVault()
    {
        VaultSettings settings = _vault.GetSettings();

        Assert.True(settings.FollowSystemTheme);
        Assert.True(settings.AutoCheckDependencies);
        Assert.True(settings.ConfirmBeforeInstall);
    }

    [Fact]
    public void SaveSettings_PersistsChanges()
    {
        var settings = _vault.GetSettings();
        settings.FollowSystemTheme = false;
        _vault.SaveSettings(settings);

        var reloaded = _vault.GetSettings();
        Assert.False(reloaded.FollowSystemTheme);
    }

    [Fact]
    public void UpsertAndDeleteCatalogItem_WorksCorrectly()
    {
        var item = new CatalogItem("Test App", "Test.App", "Test", false, "UnitTest");
        _vault.UpsertCatalogItem(item);

        var items = _vault.GetCatalogItems();
        Assert.Contains(items, i => i.PackageId == "Test.App");

        _vault.DeleteCatalogItem("Test.App");
        items = _vault.GetCatalogItems();
        Assert.DoesNotContain(items, i => i.PackageId == "Test.App");
    }

    [Fact]
    public void ImportCatalogFromJson_ImportsItems()
    {
        string json = """
        [
          { "name": "Imported App", "packageId": "Imported.App", "category": "Test", "isEssential": true, "sourceName": "JsonImport" }
        ]
        """;

        int count = _vault.ImportCatalogFromJson(json);

        Assert.Equal(1, count);
        var items = _vault.GetCatalogItems();
        Assert.Contains(items, i => i.PackageId == "Imported.App" && i.Name == "Imported App");
    }

    [Fact]
    public void WriteLog_AndGetRecentLogs_WorkCorrectly()
    {
        var entry = new BananaSuisa.Core.Logging.JsonLogEntry(
            Guid.NewGuid(), "0.0.1", 1, 1, DateTime.UtcNow,
            "Information", "test", "Hello vault", null, null);

        _vault.WriteLog(entry);

        var logs = _vault.GetRecentLogs(10);
        Assert.Contains(logs, l => l.Message == "Hello vault");
    }

    [Fact]
    public void GetAuditTrail_ContainsSeedEntry()
    {
        var trail = _vault.GetAuditTrail(10);

        Assert.Contains(trail, e => e.Operation == "seed");
    }

    [Fact]
    public void ExportAllToJson_ReturnsValidJson()
    {
        string json = _vault.ExportAllToJson();

        Assert.Contains("metadata", json);
        Assert.Contains("catalogo", json);
        Assert.Contains("settings", json);
    }

    public void Dispose()
    {
        _vault.Dispose();
        string? dir = Path.GetDirectoryName(_vaultPath);
        if (dir is not null && Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }
}
