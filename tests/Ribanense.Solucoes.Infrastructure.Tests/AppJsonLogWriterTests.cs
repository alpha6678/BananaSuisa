using Ribanense.Solucoes.Infrastructure.Logging;
using Ribanense.Solucoes.PluginSDK.Logging;
using Xunit;

namespace Ribanense.Solucoes.Infrastructure.Tests;

public class AppJsonLogWriterTests
{
    [Fact]
    public void Write_persists_entry_with_session_version_and_pid()
    {
        using var fx = new VaultFixture();

        var session = Guid.NewGuid();
        var writer = new AppJsonLogWriter(fx.Vault, session, "1.2.3", 7777);

        writer.Write(AppLogLevel.Information, "startup", "Sessão iniciada.");

        var logs = fx.Vault.GetLogsBySession(session);
        Assert.Single(logs);

        var entry = logs[0];
        Assert.Equal(session, entry.SessionId);
        Assert.Equal("1.2.3", entry.AppVersion);
        Assert.Equal(7777, entry.ProcessId);
        Assert.Equal("startup", entry.Category);
        Assert.Equal("Sessão iniciada.", entry.Message);
        Assert.Equal("Information", entry.Level);
        Assert.Null(entry.Exception);
    }

    [Fact]
    public void Write_with_exception_records_stack()
    {
        using var fx = new VaultFixture();
        var writer = new AppJsonLogWriter(fx.Vault, Guid.NewGuid(), "1.0.0", 1);

        try
        {
            throw new InvalidOperationException("boom");
        }
        catch (Exception ex)
        {
            writer.Write(AppLogLevel.Error, "test", "algo falhou", ex);
        }

        var log = fx.Vault.GetRecentLogs(1)[0];
        Assert.NotNull(log.Exception);
        Assert.Contains("InvalidOperationException", log.Exception!);
        Assert.Contains("boom", log.Exception!);
    }

    [Fact]
    public void Write_includes_custom_data()
    {
        using var fx = new VaultFixture();
        var writer = new AppJsonLogWriter(fx.Vault, Guid.NewGuid(), "1.0.0", 1);

        writer.Write(AppLogLevel.Warning, "install", "Pacote adiado", data: new Dictionary<string, string>
        {
            ["packageId"] = "Foo.Bar",
            ["reason"] = "rede"
        });

        var log = fx.Vault.GetRecentLogs(1)[0];
        Assert.NotNull(log.Data);
        Assert.Equal("Foo.Bar", log.Data!["packageId"]);
        Assert.Equal("rede", log.Data!["reason"]);
    }

    [Fact]
    public void Writer_with_null_vault_throws()
    {
        Assert.Throws<ArgumentNullException>(() => new AppJsonLogWriter(null!));
    }
}
