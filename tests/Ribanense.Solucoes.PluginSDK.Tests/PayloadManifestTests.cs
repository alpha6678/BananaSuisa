using System.IO.Compression;
using System.Text;
using Ribanense.Solucoes.PluginSDK.Payload;
using Xunit;

namespace Ribanense.Solucoes.PluginSDK.Tests;

public class PayloadManifestTests
{
    [Fact]
    public void Compute_hashes_files_in_stable_order_and_skips_metadata()
    {
        using var root = new TempDir();
        File.WriteAllText(Path.Combine(root.Path, "b.txt"), "beta");
        Directory.CreateDirectory(Path.Combine(root.Path, "sub"));
        File.WriteAllText(Path.Combine(root.Path, "sub", "a.txt"), "alfa");
        Directory.CreateDirectory(Path.Combine(root.Path, "dados"));
        File.WriteAllText(Path.Combine(root.Path, "dados", "segredo.txt"), "nao entra");
        File.WriteAllText(Path.Combine(root.Path, PayloadManifest.FileName), "{velho}");
        File.WriteAllText(Path.Combine(root.Path, PayloadManifest.PortableMarkerFileName), "1");

        var manifest = PayloadManifest.Compute(root.Path, "1.2.3", "App.exe");

        Assert.Equal("1.2.3", manifest.Version);
        Assert.Equal(new[] { "b.txt", "sub/a.txt" }, manifest.Files.Select(f => f.Path).ToArray());
        Assert.Equal(4, manifest.Files[0].Size);
        Assert.Equal(64, manifest.Files[0].Sha256.Length);
        Assert.NotEqual(manifest.Files[0].Sha256, manifest.Files[1].Sha256);
    }

    [Fact]
    public void Serialize_roundtrips()
    {
        using var root = new TempDir();
        File.WriteAllBytes(Path.Combine(root.Path, "App.exe"), new byte[] { 1, 2, 3 });

        var manifest = PayloadManifest.Compute(root.Path, "0.1.0", "App.exe");
        var again = PayloadManifest.Parse(manifest.Serialize());

        Assert.Equal(manifest.Version, again.Version);
        Assert.Equal(manifest.EntryExecutable, again.EntryExecutable);
        Assert.Equal(manifest.Files[0].Path, again.Files[0].Path);
        Assert.Equal(manifest.Files[0].Sha256, again.Files[0].Sha256);
        Assert.Equal(manifest.Files[0].Size, again.Files[0].Size);
    }

    [Fact]
    public void Diff_detects_add_change_and_delete()
    {
        var from = new PayloadManifest
        {
            Version = "1.0.0",
            EntryExecutable = "App.exe",
            Files = new List<PayloadFileEntry>
            {
                Entry("fica.txt", "aaa"),
                Entry("muda.txt", "velho"),
                Entry("sai.txt", "x")
            }
        };
        var to = new PayloadManifest
        {
            Version = "1.1.0",
            EntryExecutable = "App.exe",
            Files = new List<PayloadFileEntry>
            {
                Entry("fica.txt", "aaa"),
                Entry("muda.txt", "novo"),
                Entry("entra.txt", "y")
            }
        };

        var delta = DeltaManifest.Diff(from, to);

        Assert.Equal("1.0.0", delta.FromVersion);
        Assert.Equal("1.1.0", delta.ToVersion);
        Assert.Equal(new[] { "entra.txt", "muda.txt" }, delta.Files.Select(f => f.Path).ToArray());
        Assert.Equal(new[] { "sai.txt" }, delta.Deleted);
    }

    [Theory]
    [InlineData("delta-from-1.2.3.zip", "1.2.3")]
    [InlineData("delta-from-1.2.3-beta.1.zip", "1.2.3-beta.1")]
    [InlineData("delta-from-1.2.3.zip.sha256", null)]
    [InlineData("app-1.2.3-win-x64.zip", null)]
    public void TryParseFromVersion_reads_only_the_delta_zip(string name, string? expected)
    {
        Assert.Equal(expected, DeltaManifest.TryParseFromVersion(name));
    }

    private static PayloadFileEntry Entry(string path, string content)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        return new PayloadFileEntry
        {
            Path = path,
            Size = bytes.Length,
            Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant()
        };
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rb-payload-" + Guid.NewGuid().ToString("N"));

        public TempDir() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { }
        }
    }
}
