namespace BananaSuisa.Core.Provisioning;

public sealed record UwpAppInstallerProbeResult(
    bool AppInstallerPackageFound,
    string? AppInstallerPackageFullName,
    string? AppInstallerVersion,
    bool StorePackageFound,
    string? StoreVersion,
    bool IsHealthy,
    string Summary);
