using System.IO.Compression;
using System.Text;
using Ribanense.Solucoes.PluginSDK.Payload;
using Xunit;

namespace Ribanense.Solucoes.PluginSDK.Tests;

public class PayloadPlannerTests
{
    [Fact]
    public void Plan_chains_each_published_delta_until_the_target()
    {
        var releases = new[]
        {
            Rel("1.0.1", "1.0.0"),
            Rel("1.0.2", "1.0.1"),
            Rel("1.0.3", "1.0.2")
        };

        var plan = PayloadPlanner.Plan("1.0.0", "1.0.3", releases);

        Assert.Equal(PayloadPlanKind.Chain, plan.Kind);
        Assert.Equal(new[] { "1.0.1", "1.0.2", "1.0.3" }, plan.Hops.Select(h => h.ToVersion).ToArray());
        Assert.Equal("1.0.0", plan.Hops[0].FromVersion);
        Assert.Equal("1.0.2", plan.Hops[2].FromVersion);
    }

    [Fact]
    public void Plan_falls_back_to_full_when_a_link_is_missing()
    {
        var releases = new[]
        {
            Rel("1.0.1", "1.0.0"),
            new PayloadReleaseRef("1.0.2", true, Array.Empty<string>())
        };

        var plan = PayloadPlanner.Plan("1.0.0", "1.0.2", releases);

        Assert.Equal(PayloadPlanKind.Full, plan.Kind);
        Assert.Empty(plan.Hops);
    }

    [Fact]
    public void Plan_falls_back_to_full_when_the_chain_is_too_long()
    {
        var releases = new[]
        {
            Rel("1.0.1", "1.0.0"),
            Rel("1.0.2", "1.0.1"),
            Rel("1.0.3", "1.0.2")
        };

        var plan = PayloadPlanner.Plan("1.0.0", "1.0.3", releases, maxChain: 2);

        Assert.Equal(PayloadPlanKind.Full, plan.Kind);
    }

    [Fact]
    public void Plan_is_full_when_nothing_is_installed()
    {
        var plan = PayloadPlanner.Plan(null, "1.0.1", new[] { Rel("1.0.1", "1.0.0") });

        Assert.Equal(PayloadPlanKind.Full, plan.Kind);
    }

    [Fact]
    public void Plan_is_empty_chain_when_already_on_the_target()
    {
        var plan = PayloadPlanner.Plan("1.0.1", "1.0.1", new[] { Rel("1.0.1", "1.0.0") });

        Assert.True(plan.IsEmptyChain);
    }

    [Fact]
    public void Apply_delta_keeps_unchanged_files_and_writes_the_new_manifest()
    {
        using var previous = new TempDir();
        using var next = new TempDir();
        using var staging = new TempDir();

        File.WriteAllText(Path.Combine(previous.Path, "fica.txt"), "igual");
        File.WriteAllText(Path.Combine(previous.Path, "muda.txt"), "velho");
        File.WriteAllText(Path.Combine(previous.Path, "sai.txt"), "x");
        var from = PayloadManifest.Compute(previous.Path, "1.0.0", "App.exe");
        from.Save(Path.Combine(previous.Path, PayloadManifest.FileName));

        File.WriteAllText(Path.Combine(next.Path, "fica.txt"), "igual");
        File.WriteAllText(Path.Combine(next.Path, "muda.txt"), "novo");
        File.WriteAllText(Path.Combine(next.Path, "entra.txt"), "y");
        var to = PayloadManifest.Compute(next.Path, "1.1.0", "App.exe");
        to.Save(Path.Combine(next.Path, PayloadManifest.FileName));

        var delta = DeltaManifest.Diff(from, to);
        byte[] zip = PayloadApplier.CreateDeltaZip(next.Path, delta);

        PayloadApplier.CopyTree(previous.Path, staging.Path);
        PayloadApplier.ApplyDelta(staging.Path, zip, delta);

        Assert.Equal("igual", File.ReadAllText(Path.Combine(staging.Path, "fica.txt")));
        Assert.Equal("novo", File.ReadAllText(Path.Combine(staging.Path, "muda.txt")));
        Assert.Equal("y", File.ReadAllText(Path.Combine(staging.Path, "entra.txt")));
        Assert.False(File.Exists(Path.Combine(staging.Path, "sai.txt")));

        var installed = PayloadManifest.Load(Path.Combine(staging.Path, PayloadManifest.FileName));
        Assert.Equal("1.1.0", installed.Version);
        Assert.Empty(PayloadApplier.Verify(staging.Path, installed));
    }

    [Fact]
    public void Extract_rejects_zip_slip()
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("../fora.txt");
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write("nao");
        }

        using var dest = new TempDir();
        Assert.Throws<InvalidDataException>(() => PayloadApplier.ExtractZip(ms.ToArray(), dest.Path));
        Assert.False(File.Exists(Path.Combine(Directory.GetParent(dest.Path)!.FullName, "fora.txt")));
    }

    private static PayloadReleaseRef Rel(string version, string deltaFrom) =>
        new(version, true, new[] { deltaFrom });

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "rb-plan-" + Guid.NewGuid().ToString("N"));

        public TempDir() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { }
        }
    }
}
