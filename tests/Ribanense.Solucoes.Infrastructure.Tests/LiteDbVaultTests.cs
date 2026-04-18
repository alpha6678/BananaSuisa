using Ribanense.Solucoes.PluginSDK.Logging;
using Xunit;

namespace Ribanense.Solucoes.Infrastructure.Tests;

public class LiteDbVaultTests
{
    [Fact]
    public void Seed_creates_metadata_with_schema_version_1()
    {
        using var fx = new VaultFixture();

        var meta = fx.Vault.GetMetadata();
        Assert.Equal(1, meta.SchemaVersion);
        Assert.NotEqual(default, meta.CreatedAtUtc);
    }

    [Fact]
    public void SetSetting_and_GetSetting_roundtrips_string()
    {
        using var fx = new VaultFixture();

        fx.Vault.SetSetting("tema", "Fluent");
        Assert.Equal("Fluent", fx.Vault.GetSetting("tema"));
    }

    [Fact]
    public void SetSetting_generic_serializes_json()
    {
        using var fx = new VaultFixture();

        var payload = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };
        fx.Vault.SetSetting("counts", payload);

        var back = fx.Vault.GetSetting<Dictionary<string, int>>("counts");
        Assert.NotNull(back);
        Assert.Equal(1, back!["a"]);
        Assert.Equal(2, back["b"]);
    }

    [Fact]
    public void GetSetting_missing_key_returns_null()
    {
        using var fx = new VaultFixture();
        Assert.Null(fx.Vault.GetSetting("nao-existe"));
    }

    [Fact]
    public void RemoveSetting_deletes_and_returns_true()
    {
        using var fx = new VaultFixture();

        fx.Vault.SetSetting("k", "v");
        Assert.True(fx.Vault.RemoveSetting("k"));
        Assert.Null(fx.Vault.GetSetting("k"));

        Assert.False(fx.Vault.RemoveSetting("k"));
    }

    [Fact]
    public void GetAllSettings_lists_all_persisted_entries()
    {
        using var fx = new VaultFixture();

        fx.Vault.SetSetting("a", "1");
        fx.Vault.SetSetting("b", "2");

        var all = fx.Vault.GetAllSettings();
        Assert.Equal(2, all.Count);
        Assert.Equal("1", all["a"]);
        Assert.Equal("2", all["b"]);
    }

    [Fact]
    public void WriteLog_and_GetRecentLogs_in_order()
    {
        using var fx = new VaultFixture();

        var session = Guid.NewGuid();
        for (int i = 0; i < 5; i++)
        {
            fx.Vault.WriteLog(new JsonLogEntry(
                session,
                "1.0.0",
                42,
                1,
                DateTime.UtcNow,
                AppLogLevel.Information.ToString(),
                "test",
                $"msg-{i}",
                null,
                null));
        }

        var recent = fx.Vault.GetRecentLogs(100);
        Assert.Equal(5, recent.Count);
        Assert.Equal("msg-0", recent[0].Message);
        Assert.Equal("msg-4", recent[^1].Message);
    }

    [Fact]
    public void GetLogsBySession_filters_correctly()
    {
        using var fx = new VaultFixture();

        var sessionA = Guid.NewGuid();
        var sessionB = Guid.NewGuid();

        fx.Vault.WriteLog(Log(sessionA, "a1"));
        fx.Vault.WriteLog(Log(sessionB, "b1"));
        fx.Vault.WriteLog(Log(sessionA, "a2"));

        var onlyA = fx.Vault.GetLogsBySession(sessionA);
        Assert.Equal(2, onlyA.Count);
        Assert.All(onlyA, e => Assert.Equal(sessionA, e.SessionId));
    }

    [Fact]
    public void Audit_trail_records_set_operations()
    {
        using var fx = new VaultFixture();

        fx.Vault.SetSetting("a", "1");
        fx.Vault.SetSetting("b", "2");

        var audit = fx.Vault.GetAuditTrail(100);
        Assert.Contains(audit, e => e.Operation == "set" && e.EntityId == "a");
        Assert.Contains(audit, e => e.Operation == "set" && e.EntityId == "b");
    }

    [Fact]
    public void ExportAllToJson_includes_metadata_and_settings()
    {
        using var fx = new VaultFixture();
        fx.Vault.SetSetting("tema", "dark");

        string json = fx.Vault.ExportAllToJson();

        Assert.Contains("metadata", json);
        Assert.Contains("settings", json);
        Assert.Contains("tema", json);
        Assert.Contains("dark", json);
    }

    private static JsonLogEntry Log(Guid session, string msg) => new(
        session, "1.0.0", 42, 1, DateTime.UtcNow,
        AppLogLevel.Information.ToString(), "test", msg, null, null);
}
