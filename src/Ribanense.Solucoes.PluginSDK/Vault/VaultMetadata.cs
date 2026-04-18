namespace Ribanense.Solucoes.PluginSDK.Vault;

public sealed class VaultMetadata
{
    public int Id { get; set; } = 1;
    public int SchemaVersion { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastModifiedAtUtc { get; set; }
}
