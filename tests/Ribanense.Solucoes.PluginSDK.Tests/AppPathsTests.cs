using Ribanense.Solucoes.PluginSDK;
using Xunit;

namespace Ribanense.Solucoes.PluginSDK.Tests;

public class AppPathsTests
{
    [Fact]
    public void Resolve_uses_env_vars_when_present()
    {
        string tempHome = Path.Combine(Path.GetTempPath(), "ribanense-tests", Guid.NewGuid().ToString("N"), "home");
        string tempData = Path.Combine(Path.GetTempPath(), "ribanense-tests", Guid.NewGuid().ToString("N"), "data");

        Directory.CreateDirectory(tempHome);
        Environment.SetEnvironmentVariable("RIBANENSE_APP_HOME", tempHome);
        Environment.SetEnvironmentVariable("RIBANENSE_APP_DATA", tempData);
        try
        {
            var paths = AppPaths.Resolve("com.ribanense.winget");

            Assert.Equal("com.ribanense.winget", paths.AppId);
            Assert.Equal(Path.GetFullPath(tempHome), paths.AppHome);
            Assert.Equal(Path.GetFullPath(tempData), paths.AppData);
            Assert.EndsWith("winget.dat", paths.VaultPath);
            Assert.True(Directory.Exists(paths.AppData));
        }
        finally
        {
            Environment.SetEnvironmentVariable("RIBANENSE_APP_HOME", null);
            Environment.SetEnvironmentVariable("RIBANENSE_APP_DATA", null);
            TryCleanup(tempHome);
            TryCleanup(tempData);
        }
    }

    [Fact]
    public void Resolve_falls_back_to_local_app_data_when_env_missing()
    {
        Environment.SetEnvironmentVariable("RIBANENSE_APP_HOME", null);
        Environment.SetEnvironmentVariable("RIBANENSE_APP_DATA", null);

        var paths = AppPaths.Resolve("com.ribanense.teste-fallback");

        Assert.Equal("com.ribanense.teste-fallback", paths.AppId);
        Assert.Contains("Ribanense Soluções", paths.AppData);
        Assert.Contains("com.ribanense.teste-fallback", paths.AppData);
        Assert.EndsWith("teste-fallback.dat", paths.VaultPath);

        TryCleanup(paths.AppData);
    }

    [Fact]
    public void Resolve_accepts_custom_vault_filename()
    {
        Environment.SetEnvironmentVariable("RIBANENSE_APP_DATA", null);
        var paths = AppPaths.Resolve("com.ribanense.teste-custom", "Custom.dat");
        Assert.EndsWith("Custom.dat", paths.VaultPath);
        TryCleanup(paths.AppData);
    }

    [Fact]
    public void Resolve_empty_app_id_throws()
    {
        Assert.Throws<ArgumentException>(() => AppPaths.Resolve(""));
        Assert.Throws<ArgumentException>(() => AppPaths.Resolve("   "));
    }

    private static void TryCleanup(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // best effort
        }
    }
}
