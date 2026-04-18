using Ribanense.Solucoes.Launcher.Services;
using Ribanense.Solucoes.Launcher.Tests.Helpers;
using Xunit;

namespace Ribanense.Solucoes.Launcher.Tests;

public class InstalledAppsRegistryTests
{
    private const string ValidManifest = """
        {
          "id": "com.ribanense.winget",
          "name": "Gestor WinGet",
          "publicName": "Gestor WinGet",
          "version": "1.0.0",
          "minimumLauncherVersion": "1.0.0",
          "entryExecutable": "App.exe",
          "githubTagPrefix": "winget-v"
        }
        """;

    [Fact]
    public void Scan_missing_root_returns_empty()
    {
        var reg = new InstalledAppsRegistry();
        Assert.Empty(reg.Scan(@"C:\__nao_existe_com_certeza__"));
    }

    [Fact]
    public void Scan_ignores_folders_without_app_json()
    {
        using var tmp = new TempFolder();
        tmp.Sub("Sozinho"); // sem app.json
        var reg = new InstalledAppsRegistry();
        Assert.Empty(reg.Scan(tmp.Path));
    }

    [Fact]
    public void Scan_returns_valid_app()
    {
        using var tmp = new TempFolder();
        string dir = tmp.Sub("Winget");
        File.WriteAllText(Path.Combine(dir, "app.json"), ValidManifest);
        File.WriteAllBytes(Path.Combine(dir, "App.exe"), new byte[] { 0x4D, 0x5A });

        var reg = new InstalledAppsRegistry();
        var list = reg.Scan(tmp.Path);

        Assert.Single(list);
        Assert.Equal("com.ribanense.winget", list[0].Id);
        Assert.EndsWith("App.exe", list[0].ExecutablePath);
    }

    [Fact]
    public void Scan_skips_app_without_exe_on_disk()
    {
        using var tmp = new TempFolder();
        string dir = tmp.Sub("Winget");
        File.WriteAllText(Path.Combine(dir, "app.json"), ValidManifest);
        // exe faltando

        var reg = new InstalledAppsRegistry();
        Assert.Empty(reg.Scan(tmp.Path));
    }

    [Fact]
    public void Scan_skips_malformed_manifest()
    {
        using var tmp = new TempFolder();
        string dir = tmp.Sub("Winget");
        File.WriteAllText(Path.Combine(dir, "app.json"), "nao-e-json-valido");
        File.WriteAllBytes(Path.Combine(dir, "App.exe"), new byte[] { 0x4D, 0x5A });

        var reg = new InstalledAppsRegistry();
        Assert.Empty(reg.Scan(tmp.Path));
    }

    [Fact]
    public void Find_returns_matching_id()
    {
        using var tmp = new TempFolder();
        string dir = tmp.Sub("Winget");
        File.WriteAllText(Path.Combine(dir, "app.json"), ValidManifest);
        File.WriteAllBytes(Path.Combine(dir, "App.exe"), new byte[] { 0x4D, 0x5A });

        var reg = new InstalledAppsRegistry();
        var found = reg.Find(tmp.Path, "com.ribanense.winget");

        Assert.NotNull(found);
        Assert.Equal("com.ribanense.winget", found!.Id);
        Assert.Null(reg.Find(tmp.Path, "nao-existe"));
    }
}
