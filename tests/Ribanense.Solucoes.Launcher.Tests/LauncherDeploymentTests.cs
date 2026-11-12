using Ribanense.Solucoes.Launcher.Domain;
using Ribanense.Solucoes.Launcher.Services;
using Ribanense.Solucoes.Launcher.Tests.Helpers;
using Ribanense.Solucoes.PluginSDK.Payload;
using Xunit;

namespace Ribanense.Solucoes.Launcher.Tests;

public class LauncherDeploymentTests
{
    [Fact]
    public void Foldered_copy_uses_delta_and_single_file_does_not()
    {
        var release = new ReleaseInfo
        {
            Tag = "launcher-v1.0.1",
            Version = "1.0.1",
            Name = "Launcher",
            IsPrerelease = false,
            PublishedAtUtc = DateTime.UtcNow,
            Assets = new[]
            {
                new ReleaseAsset { Name = "launcher-1.0.1-win-x64.zip", DownloadUrl = "https://example.com/z.zip", Size = 1 }
            }
        };

        Assert.True(LauncherUpdateService.ShouldUseDelta(release, installedCopy: true));
        Assert.False(LauncherUpdateService.ShouldUseDelta(release, installedCopy: false));
    }

    [Fact]
    public void Release_without_zip_never_uses_delta()
    {
        var release = new ReleaseInfo
        {
            Tag = "launcher-v1.0.1",
            Version = "1.0.1",
            Name = "Launcher",
            IsPrerelease = false,
            PublishedAtUtc = DateTime.UtcNow,
            Assets = new[]
            {
                new ReleaseAsset { Name = "launcher-1.0.1-win-x64.exe", DownloadUrl = "https://example.com/z.exe", Size = 1 }
            }
        };

        Assert.False(LauncherUpdateService.ShouldUseDelta(release, installedCopy: true));
        Assert.Equal("launcher-1.0.1-win-x64.exe", release.ExeAsset!.Name);
        Assert.Null(release.ZipAsset);
    }

    [Fact]
    public void HasPayloadManifest_detects_managed_folder()
    {
        using var managed = new TempFolder();
        using var loose = new TempFolder();
        File.WriteAllText(Path.Combine(managed.Path, PayloadManifest.FileName), "{}");

        Assert.True(LauncherUpdateService.HasPayloadManifest(managed.Path));
        Assert.False(LauncherUpdateService.HasPayloadManifest(loose.Path));
        Assert.False(LauncherUpdateService.HasPayloadManifest(null));
    }

    [Fact]
    public void Portable_layout_puts_data_and_apps_beside_the_executable()
    {
        using var folder = new TempFolder();
        File.WriteAllText(Path.Combine(folder.Path, PayloadManifest.PortableMarkerFileName), "1");
        string exe = Path.Combine(folder.Path, "Ribanense.Solucoes.Launcher.exe");
        File.WriteAllBytes(exe, new byte[] { 0x4D, 0x5A });

        var context = Configuration.DeploymentContext.Resolve(exe);

        Assert.Equal(Configuration.DeploymentMode.Portable, context.Mode);
        Assert.Equal(Path.Combine(folder.Path, "dados"), context.DataRoot);
        Assert.Equal(Path.Combine(folder.Path, "aplicativos"), context.AplicativosRoot);
    }

    [Fact]
    public void Environment_override_wins_over_portable_marker()
    {
        using var folder = new TempFolder();
        File.WriteAllText(Path.Combine(folder.Path, PayloadManifest.PortableMarkerFileName), "1");
        string exe = Path.Combine(folder.Path, "Ribanense.Solucoes.Launcher.exe");
        File.WriteAllBytes(exe, new byte[] { 0x4D, 0x5A });

        var context = Configuration.DeploymentContext.Resolve(exe, @"C:\dados-teste", @"C:\dados-teste\aplicativos");

        Assert.Equal(@"C:\dados-teste", context.DataRoot);
        Assert.Equal(@"C:\dados-teste\aplicativos", context.AplicativosRoot);
    }

    [Fact]
    public void Registered_mode_writes_install_mode_and_unregisters_on_portable()
    {
        using var folder = new TempFolder();
        var registration = new RecordingRegistration();
        var service = new InstallRegistrationService(registration);

        service.ApplyApp(
            AppInstallMode.Registered,
            "com.ribanense.winget",
            "Gestor WinGet",
            "1.0.0",
            folder.Path,
            Path.Combine(folder.Path, "App.exe"),
            @"C:\launcher\Ribanense.Solucoes.Launcher.exe");

        Assert.Equal(AppInstallMode.Registered, InstallRegistrationService.ReadMode(folder.Path));
        Assert.Single(registration.Registered);
        Assert.Contains("--uninstall-app com.ribanense.winget", registration.Registered[0].UninstallString);

        service.ApplyApp(
            AppInstallMode.Portable,
            "com.ribanense.winget",
            "Gestor WinGet",
            "1.0.0",
            folder.Path,
            Path.Combine(folder.Path, "App.exe"),
            @"C:\launcher\Ribanense.Solucoes.Launcher.exe");

        Assert.Equal(AppInstallMode.Portable, InstallRegistrationService.ReadMode(folder.Path));
        Assert.Contains(InstallRegistrationService.KeyForApp("com.ribanense.winget"), registration.Unregistered);
    }

    [Theory]
    [InlineData("0.2.9", "0.2.9")]
    [InlineData("0.2.9\r\n", "0.2.9")]
    [InlineData("{\"version\":\"1.4.2\",\"sdk\":\"1.0.0\"}", "1.4.2")]
    [InlineData("1.2.3.4", "1.2.3")]
    [InlineData("texto sem versao", null)]
    public void NormalizeVersion_reads_what_partner_tools_print(string raw, string? expected)
    {
        Assert.Equal(expected, AppInstallService.NormalizeVersion(raw));
    }

    private sealed class RecordingRegistration : IInstallRegistration
    {
        public List<InstallRegistration> Registered { get; } = new();
        public List<string> Unregistered { get; } = new();

        public void Register(InstallRegistration registration) => Registered.Add(registration);

        public void Unregister(string keyName) => Unregistered.Add(keyName);

        public InstallRegistration? Find(string keyName) => null;

        public IReadOnlyList<InstallRegistration> FindByDisplayName(string displayName) =>
            Array.Empty<InstallRegistration>();
    }
}
