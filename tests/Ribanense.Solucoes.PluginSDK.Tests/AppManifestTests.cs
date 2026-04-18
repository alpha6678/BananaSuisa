using Ribanense.Solucoes.PluginSDK.Manifest;
using Xunit;

namespace Ribanense.Solucoes.PluginSDK.Tests;

public class AppManifestTests
{
    private const string ValidJson = """
        {
          "id": "com.ribanense.winget",
          "name": "Gestor WinGet",
          "publicName": "Gestor WinGet",
          "version": "1.2.3",
          "minimumLauncherVersion": "1.0.0",
          "entryExecutable": "Ribanense.Solucoes.App.Winget.exe",
          "icon": "icon.png",
          "category": "Pacotes",
          "requiresElevation": false,
          "githubTagPrefix": "winget-v"
        }
        """;

    [Fact]
    public void Parse_valid_returns_populated_manifest()
    {
        var m = AppManifest.Parse(ValidJson);

        Assert.Equal("com.ribanense.winget", m.Id);
        Assert.Equal("Gestor WinGet", m.Name);
        Assert.Equal("1.2.3", m.Version);
        Assert.Equal("1.0.0", m.MinimumLauncherVersion);
        Assert.Equal("Ribanense.Solucoes.App.Winget.exe", m.EntryExecutable);
        Assert.Equal("winget-v", m.GithubTagPrefix);
        Assert.False(m.RequiresElevation);
    }

    [Fact]
    public void Validate_valid_manifest_has_no_errors()
    {
        var m = AppManifest.Parse(ValidJson);
        Assert.Empty(m.Validate());
    }

    [Fact]
    public void Validate_empty_manifest_lists_required_fields()
    {
        var m = new AppManifest();
        var errors = m.Validate();

        Assert.Contains(errors, e => e.Contains("id"));
        Assert.Contains(errors, e => e.Contains("name"));
        Assert.Contains(errors, e => e.Contains("publicName"));
        Assert.Contains(errors, e => e.Contains("version"));
        Assert.Contains(errors, e => e.Contains("minimumLauncherVersion"));
        Assert.Contains(errors, e => e.Contains("entryExecutable"));
        Assert.Contains(errors, e => e.Contains("githubTagPrefix"));
    }

    [Fact]
    public void Validate_invalid_semver_is_reported()
    {
        string bad = ValidJson.Replace("\"1.2.3\"", "\"versao-errada\"");
        var m = AppManifest.Parse(bad);
        var errors = m.Validate();

        Assert.Contains(errors, e => e.Contains("version"));
    }

    [Fact]
    public void Serialize_roundtrips()
    {
        var m = AppManifest.Parse(ValidJson);
        string json = m.Serialize();
        var again = AppManifest.Parse(json);

        Assert.Equal(m.Id, again.Id);
        Assert.Equal(m.Version, again.Version);
        Assert.Equal(m.EntryExecutable, again.EntryExecutable);
    }

    [Fact]
    public void Parse_empty_input_throws()
    {
        Assert.Throws<ArgumentException>(() => AppManifest.Parse(""));
    }
}
