using Ribanense.Solucoes.Launcher.Domain;

namespace Ribanense.Solucoes.Launcher.Services;

public interface IReleaseCheckService
{
    Task<IReadOnlyList<ReleaseInfo>> GetAllReleasesAsync(
        string owner, string repo, string tagPrefix, CancellationToken ct);

    Task<ReleaseInfo?> GetLatestReleaseAsync(
        string owner, string repo, string tagPrefix, bool includePrerelease, CancellationToken ct);

    UpdateStatus CompareVersions(string? installedVersion, string? latestVersion);
}
