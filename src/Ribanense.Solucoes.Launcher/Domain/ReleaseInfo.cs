using Ribanense.Solucoes.PluginSDK.Payload;

namespace Ribanense.Solucoes.Launcher.Domain;

public sealed class ReleaseInfo
{
    public required string Tag { get; init; }
    public required string Version { get; init; }
    public required string Name { get; init; }
    public bool IsPrerelease { get; init; }
    public DateTime PublishedAtUtc { get; init; }
    public required IReadOnlyList<ReleaseAsset> Assets { get; init; }

    public ReleaseAsset? ZipAsset =>
        Assets.FirstOrDefault(a =>
            a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
            !a.Name.EndsWith(".zip.sha256", StringComparison.OrdinalIgnoreCase) &&
            DeltaManifest.TryParseFromVersion(a.Name) is null);

    public ReleaseAsset? Sha256Asset =>
        ZipAsset is null
            ? null
            : Assets.FirstOrDefault(a =>
                string.Equals(a.Name, ZipAsset.Name + ".sha256", StringComparison.OrdinalIgnoreCase));

    public ReleaseAsset? ManifestAsset =>
        Assets.FirstOrDefault(a => string.Equals(a.Name, "app.json", StringComparison.OrdinalIgnoreCase));

    public ReleaseAsset? PayloadManifestAsset =>
        Assets.FirstOrDefault(a =>
            string.Equals(a.Name, PayloadManifest.FileName, StringComparison.OrdinalIgnoreCase));

    public ReleaseAsset? ExeAsset =>
        Assets.FirstOrDefault(a =>
            a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
            !a.Name.EndsWith(".exe.sha256", StringComparison.OrdinalIgnoreCase));

    public ReleaseAsset? ExeSha256Asset =>
        ExeAsset is null
            ? null
            : Assets.FirstOrDefault(a =>
                string.Equals(a.Name, ExeAsset.Name + ".sha256", StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<string> DeltaFromVersions =>
        Assets
            .Select(a => DeltaManifest.TryParseFromVersion(a.Name))
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

    public ReleaseAsset? DeltaZipFrom(string fromVersion) =>
        FindExact(DeltaManifest.ZipFileName(fromVersion));

    public ReleaseAsset? DeltaManifestFrom(string fromVersion) =>
        FindExact(DeltaManifest.ManifestFileName(fromVersion));

    public ReleaseAsset? DeltaShaFrom(string fromVersion) =>
        FindExact(DeltaManifest.ShaFileName(fromVersion));

    public ReleaseAsset? FindByPattern(string? pattern) =>
        string.IsNullOrWhiteSpace(pattern) ? null : FindExact(ApplyVersionPattern(pattern, Version));

    public PayloadReleaseRef ToPayloadRef() =>
        new(Version, ZipAsset is not null, DeltaFromVersions);

    public static string ApplyVersionPattern(string pattern, string version) =>
        pattern.Replace("{version}", version, StringComparison.OrdinalIgnoreCase);

    private ReleaseAsset? FindExact(string name) =>
        Assets.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
}

public sealed class ReleaseAsset
{
    public required string Name { get; init; }
    public required string DownloadUrl { get; init; }
    public long Size { get; init; }
}
