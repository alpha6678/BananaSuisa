using System.Reflection;
using Ribanense.Solucoes.PluginSDK;
using Xunit;

namespace Ribanense.Solucoes.PluginSDK.Tests;

public class AppVersionTests
{
    [Fact]
    public void ForAssembly_reads_version_from_assembly()
    {
        var assembly = typeof(AppVersion).Assembly;
        string version = AppVersion.ForAssembly(assembly);

        // Directory.Build.props define Version SemVer no repo (ex.: 0.1.1).
        Assert.False(string.IsNullOrWhiteSpace(version));
        Assert.Matches(@"^\d+\.\d+\.\d+$", version);
    }

    [Fact]
    public void ForAssembly_null_throws()
    {
        Assert.Throws<ArgumentNullException>(() => AppVersion.ForAssembly(null!));
    }

    [Fact]
    public void ForEntry_returns_non_empty_version()
    {
        string version = AppVersion.ForEntry();
        Assert.False(string.IsNullOrWhiteSpace(version));
    }
}
