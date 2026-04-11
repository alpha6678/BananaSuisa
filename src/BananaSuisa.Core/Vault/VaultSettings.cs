namespace BananaSuisa.Core.Vault;

public sealed class VaultSettings
{
    public int Id { get; set; } = 1;
    public bool FollowSystemTheme { get; set; } = true;
    public bool AutoCheckDependencies { get; set; } = true;
    public bool ShowLogPanel { get; set; } = true;
    public bool ConfirmBeforeInstall { get; set; } = true;
    public bool AutoAcceptAgreements { get; set; } = true;
}
