namespace BananaSuisa.Core.Vault;

public sealed class VaultAuditEntry
{
    public int Id { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string Collection { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Detail { get; set; }
}
