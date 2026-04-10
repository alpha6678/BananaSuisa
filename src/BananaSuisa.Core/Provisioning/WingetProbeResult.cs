namespace BananaSuisa.Core.Provisioning;

public sealed record WingetProbeResult(
    bool IsExecutableOnPath,
    string? ExecutablePath,
    string? VersionOutput,
    string? SourceListOutput,
    int SourceListExitCode,
    bool IsHealthy,
    string Summary);
