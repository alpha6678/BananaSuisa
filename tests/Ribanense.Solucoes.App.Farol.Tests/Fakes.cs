using Ribanense.Solucoes.App.Farol.Domain;
using Ribanense.Solucoes.PluginSDK.Logging;
using Ribanense.Solucoes.PluginSDK.Vault;

namespace Ribanense.Solucoes.App.Farol.Tests;

/// <summary>Vault em memória: os testes de pareamento não precisam tocar disco.</summary>
public sealed class FakeVault : IVault
{
    private readonly Dictionary<string, string> _settings = new(StringComparer.Ordinal);
    private readonly List<JsonLogEntry> _logs = new();

    public VaultMetadata GetMetadata() => new();

    public string? GetSetting(string key) =>
        _settings.TryGetValue(key, out string? value) ? value : null;

    public T? GetSetting<T>(string key) => default;

    public void SetSetting(string key, string value) => _settings[key] = value;

    public void SetSetting<T>(string key, T value) =>
        _settings[key] = value?.ToString() ?? string.Empty;

    public bool RemoveSetting(string key) => _settings.Remove(key);

    public IReadOnlyDictionary<string, string> GetAllSettings() => _settings;

    public void WriteLog(JsonLogEntry entry) => _logs.Add(entry);

    public IReadOnlyList<JsonLogEntry> GetRecentLogs(int count = 200) => _logs;

    public IReadOnlyList<JsonLogEntry> GetLogsBySession(Guid sessionId) => _logs;

    public IReadOnlyList<VaultAuditEntry> GetAuditTrail(int count = 100) =>
        Array.Empty<VaultAuditEntry>();

    public string ExportAllToJson() => "{}";

    public void Dispose()
    {
    }
}

/// <summary>Fábrica de dossiês para os testes de regra e de diff.</summary>
public static class BundleFactory
{
    public static EvidenceBundle Healthy(string name = "CAIXA-1") => new()
    {
        MachineId = name.ToLowerInvariant(),
        MachineName = name,
        FriendlyName = name,
        FarolVersion = "0.1.0",
        Collectors = new[]
        {
            new CollectorOutcome("identity", "Identidade", CollectorStatus.Ok, null, 5),
            new CollectorOutcome("network", "Rede", CollectorStatus.Ok, null, 12),
        },
        Identity = new IdentityInfo(name, "operador", "Windows 11", "X64", 4.5, "E. South America Standard Time", DateTimeOffset.Now),
        Network = new NetworkInfo(
            NetworkCategory.Private,
            "Rede da loja",
            new[] { new AdapterInfo("Ethernet", "Realtek", "Up", "AA:BB", new[] { "192.168.0.10" }, new[] { "192.168.0.1" }, new[] { "192.168.0.1" }) },
            new[] { "192.168.0.1" },
            "192.168.0.1",
            2),
        Disks = new[] { new DiskInfo(@"C:\", "Sistema", "NTFS", 500_000_000_000, 250_000_000_000) },
        Services = new[]
        {
            new ServiceInfo("Spooler", "Spooler de Impressão", "Running", "Automatic"),
            new ServiceInfo("Dnscache", "Cliente DNS", "Running", "Automatic"),
            new ServiceInfo("Winmgmt", "Instrumentação WMI", "Running", "Automatic"),
        },
        Printers = new[]
        {
            new PrinterInfo("Termica Balcao", "Generic / Text Only", "USB001", true, false, false, 0, "Ociosa"),
        },
    };
}
