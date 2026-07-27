using Ribanense.Solucoes.Launcher.Services;
using Ribanense.Solucoes.Launcher.Tests.Helpers;
using Xunit;

namespace Ribanense.Solucoes.Launcher.Tests;

public class DotNetDesktopRuntimeCheckerTests
{
    private const string VersionPlaceholder = "__VERSION__";

    private const string RuntimeConfigTemplate =
        """
        {
          "runtimeOptions": {
            "tfm": "net10.0",
            "frameworks": [
              { "name": "Microsoft.NETCore.App", "version": "10.0.0" },
              { "name": "Microsoft.WindowsDesktop.App", "version": "__VERSION__" }
            ]
          }
        }
        """;

    private static string RuntimeConfigWithVersion(string version) =>
        RuntimeConfigTemplate.Replace(VersionPlaceholder, version);

    [Fact]
    public void Check_returns_satisfied_when_runtimeconfig_is_missing()
    {
        using var temp = new TempFolder();
        string exePath = System.IO.Path.Combine(temp.Path, "App.exe");
        File.WriteAllBytes(exePath, Array.Empty<byte>());

        var checker = new DotNetDesktopRuntimeChecker(Array.Empty<string>());

        var result = checker.Check(exePath);

        Assert.True(result.IsSatisfied);
    }

    [Fact]
    public void Check_returns_satisfied_when_frameworks_section_has_no_windows_desktop_app()
    {
        using var temp = new TempFolder();
        string exePath = System.IO.Path.Combine(temp.Path, "App.exe");
        File.WriteAllBytes(exePath, Array.Empty<byte>());
        File.WriteAllText(
            System.IO.Path.Combine(temp.Path, "App.runtimeconfig.json"),
            """{ "runtimeOptions": { "tfm": "net10.0", "frameworks": [ { "name": "Microsoft.NETCore.App", "version": "10.0.0" } ] } }""");

        var checker = new DotNetDesktopRuntimeChecker(Array.Empty<string>());

        var result = checker.Check(exePath);

        Assert.True(result.IsSatisfied);
    }

    [Fact]
    public void Check_returns_not_satisfied_when_required_runtime_is_absent()
    {
        using var temp = new TempFolder();
        string exePath = System.IO.Path.Combine(temp.Path, "App.exe");
        File.WriteAllBytes(exePath, Array.Empty<byte>());
        File.WriteAllText(
            System.IO.Path.Combine(temp.Path, "App.runtimeconfig.json"),
            RuntimeConfigWithVersion("10.0.0"));

        string sharedRoot = temp.Sub("shared-empty");
        var checker = new DotNetDesktopRuntimeChecker(new[] { sharedRoot });

        var result = checker.Check(exePath);

        Assert.False(result.IsSatisfied);
        Assert.Equal("10.0.0", result.RequiredVersion);
    }

    [Fact]
    public void Check_returns_satisfied_when_matching_major_version_is_installed_with_higher_patch()
    {
        using var temp = new TempFolder();
        string exePath = System.IO.Path.Combine(temp.Path, "App.exe");
        File.WriteAllBytes(exePath, Array.Empty<byte>());
        File.WriteAllText(
            System.IO.Path.Combine(temp.Path, "App.runtimeconfig.json"),
            RuntimeConfigWithVersion("10.0.0"));

        string sharedRoot = temp.Sub("shared");
        Directory.CreateDirectory(System.IO.Path.Combine(sharedRoot, "10.0.3"));

        var checker = new DotNetDesktopRuntimeChecker(new[] { sharedRoot });

        var result = checker.Check(exePath);

        Assert.True(result.IsSatisfied);
    }

    [Fact]
    public void Check_returns_not_satisfied_when_only_a_different_major_version_is_installed()
    {
        using var temp = new TempFolder();
        string exePath = System.IO.Path.Combine(temp.Path, "App.exe");
        File.WriteAllBytes(exePath, Array.Empty<byte>());
        File.WriteAllText(
            System.IO.Path.Combine(temp.Path, "App.runtimeconfig.json"),
            RuntimeConfigWithVersion("10.0.0"));

        string sharedRoot = temp.Sub("shared");
        Directory.CreateDirectory(System.IO.Path.Combine(sharedRoot, "8.0.10"));

        var checker = new DotNetDesktopRuntimeChecker(new[] { sharedRoot });

        var result = checker.Check(exePath);

        Assert.False(result.IsSatisfied);
    }
}
