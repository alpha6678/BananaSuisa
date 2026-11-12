using Ribanense.Solucoes.Launcher.Services;
using Ribanense.Solucoes.Launcher.Tests.Helpers;
using Xunit;

namespace Ribanense.Solucoes.Launcher.Tests;

public class LauncherMaintenanceTests
{
    [Fact]
    public void App_data_root_sits_under_launcher_data()
    {
        Assert.Equal(
            Path.Combine(@"C:\dados", "apps"),
            LauncherMaintenance.AppDataRootFor(@"C:\dados"));
    }

    [Fact]
    public void Measure_counts_folders_and_bytes_of_both_trees()
    {
        using var root = new TempFolder();
        string apps = root.Sub("aplicativos", "com.ribanense.winget");
        File.WriteAllBytes(Path.Combine(apps, "app.bin"), new byte[2048]);
        string data = root.Sub("apps", "com.ribanense.winget");
        File.WriteAllBytes(Path.Combine(data, "historico.json"), new byte[1024]);

        LauncherUsage usage = LauncherMaintenance.Measure(root.Sub("aplicativos"), root.Sub("apps"));

        Assert.Equal(1, usage.InstalledApps);
        Assert.Equal(2048, usage.InstalledAppsBytes);
        Assert.Equal(1, usage.AppDataFolders);
        Assert.Equal(1024, usage.AppDataBytes);
    }

    [Fact]
    public void Measure_of_missing_folders_is_zero()
    {
        using var root = new TempFolder();

        LauncherUsage usage = LauncherMaintenance.Measure(
            Path.Combine(root.Path, "nao-existe"),
            Path.Combine(root.Path, "tambem-nao"));

        Assert.Equal(0, usage.InstalledApps);
        Assert.Equal(0, usage.InstalledAppsBytes);
        Assert.Equal(0, usage.AppDataFolders);
        Assert.Equal(0, usage.AppDataBytes);
    }

    [Fact]
    public void Clear_app_data_removes_app_folders_and_keeps_installed_apps()
    {
        using var root = new TempFolder();
        string apps = root.Sub("aplicativos", "com.ribanense.winget");
        File.WriteAllText(Path.Combine(apps, "app.exe"), "binario");
        string data = root.Sub("apps", "com.ribanense.winget", "logs");
        File.WriteAllText(Path.Combine(data, "log.json"), "{}");

        int removed = LauncherMaintenance.ClearAppData(root.Sub("apps"));

        Assert.Equal(1, removed);
        Assert.Empty(Directory.GetDirectories(Path.Combine(root.Path, "apps")));
        // O binário do app instalado continua no lugar.
        Assert.True(File.Exists(Path.Combine(apps, "app.exe")));
    }

    [Fact]
    public void Clear_app_data_on_missing_folder_is_a_no_op()
    {
        using var root = new TempFolder();

        Assert.Equal(0, LauncherMaintenance.ClearAppData(Path.Combine(root.Path, "apps")));
    }

    [Theory]
    [InlineData(0, "0 KB")]
    [InlineData(1024, "1 KB")]
    [InlineData(2_097_152, "2 MB")]
    public void Formats_sizes_for_display(long bytes, string expected)
    {
        Assert.Equal(expected, LauncherMaintenance.FormatSize(bytes));
    }
}
