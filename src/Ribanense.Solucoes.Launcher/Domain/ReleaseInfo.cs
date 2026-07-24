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
        Assets.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

    public ReleaseAsset? Sha256Asset =>
        Assets.FirstOrDefault(a => a.Name.EndsWith(".zip.sha256", StringComparison.OrdinalIgnoreCase));

    public ReleaseAsset? ManifestAsset =>
        Assets.FirstOrDefault(a => string.Equals(a.Name, "app.json", StringComparison.OrdinalIgnoreCase));

    public ReleaseAsset? ExeAsset =>
        Assets.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

    public ReleaseAsset? ExeSha256Asset =>
        Assets.FirstOrDefault(a => a.Name.EndsWith(".exe.sha256", StringComparison.OrdinalIgnoreCase));
}

public sealed class ReleaseAsset
{
    public required string Name { get; init; }
    public required string DownloadUrl { get; init; }
    public long Size { get; init; }
}
