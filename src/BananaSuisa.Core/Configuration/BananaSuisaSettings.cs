namespace BananaSuisa.Core.Configuration;

public sealed class BananaSuisaSettings
{
    public bool FollowSystemTheme { get; init; }

    public bool AutoCheckDependencies { get; init; }

    public bool ShowLogPanel { get; init; }

    public bool ConfirmBeforeInstall { get; init; }

    public bool AutoAcceptAgreements { get; init; }
}
