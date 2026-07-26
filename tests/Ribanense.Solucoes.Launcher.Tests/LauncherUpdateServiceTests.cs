using Ribanense.Solucoes.Launcher.Services;
using Xunit;

namespace Ribanense.Solucoes.Launcher.Tests;

public class LauncherUpdateServiceTests
{
    [Fact]
    public void Target_path_uses_new_release_filename_instead_of_current_filename()
    {
        string dir = Path.Combine(Path.GetTempPath(), "Ribanense");
        string currentPath = Path.Combine(dir, "launcher-0.1.11-win-x64.exe");

        string targetPath = LauncherUpdateService.GetTargetExecutablePath(
            currentPath,
            "launcher-0.1.12-win-x64.exe");

        Assert.Equal(Path.Combine(dir, "launcher-0.1.12-win-x64.exe"), targetPath);
        Assert.NotEqual(currentPath, targetPath);
    }

    [Fact]
    public void Legacy_migration_renames_first_update_installed_under_old_version()
    {
        string dir = Path.Combine(Path.GetTempPath(), "Ribanense");
        string currentPath = Path.Combine(dir, "launcher-0.1.10-win-x64.exe");

        string? targetPath = LauncherUpdateService.GetLegacyFileNameMigrationTarget(
            currentPath,
            "0.1.12");

        Assert.Equal(Path.Combine(dir, "launcher-0.1.12-win-x64.exe"), targetPath);
    }

    [Fact]
    public void Legacy_migration_does_nothing_when_filename_already_matches_version()
    {
        string currentPath = Path.Combine(
            Path.GetTempPath(),
            "Ribanense",
            "launcher-0.1.12-win-x64.exe");

        string? targetPath = LauncherUpdateService.GetLegacyFileNameMigrationTarget(
            currentPath,
            "0.1.12");

        Assert.Null(targetPath);
    }

    [Fact]
    public void Legacy_migration_preserves_custom_filename()
    {
        string currentPath = Path.Combine(
            Path.GetTempPath(),
            "Ribanense",
            "Ribanense.Solucoes.Launcher.exe");

        string? targetPath = LauncherUpdateService.GetLegacyFileNameMigrationTarget(
            currentPath,
            "0.1.12");

        Assert.Null(targetPath);
    }

    [Fact]
    public void Target_path_ignores_directories_from_asset_name()
    {
        string dir = Path.Combine(Path.GetTempPath(), "Ribanense");
        string currentPath = Path.Combine(dir, "launcher-0.1.11-win-x64.exe");

        string targetPath = LauncherUpdateService.GetTargetExecutablePath(
            currentPath,
            Path.Combine("outra-pasta", "launcher-0.1.12-win-x64.exe"));

        Assert.Equal(Path.Combine(dir, "launcher-0.1.12-win-x64.exe"), targetPath);
    }

    [Theory]
    [InlineData("")]
    [InlineData("launcher-0.1.12-win-x64.zip")]
    [InlineData("launcher-sem-extensao")]
    public void Target_path_rejects_invalid_executable_asset_name(string assetName)
    {
        string currentPath = Path.Combine(
            Path.GetTempPath(),
            "Ribanense",
            "launcher-0.1.11-win-x64.exe");

        Assert.Throws<ArgumentException>(() =>
            LauncherUpdateService.GetTargetExecutablePath(currentPath, assetName));
    }
}
