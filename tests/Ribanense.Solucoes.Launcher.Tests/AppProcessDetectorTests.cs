using Ribanense.Solucoes.Launcher.Services;
using Xunit;

namespace Ribanense.Solucoes.Launcher.Tests;

public class AppProcessDetectorTests
{
    [Fact]
    public void MutexNameFor_uses_global_prefix()
    {
        Assert.Equal(
            @"Global\Ribanense.com.ribanense.winget",
            AppProcessDetector.MutexNameFor("com.ribanense.winget"));
    }

    [Fact]
    public void TryCloseRunning_returns_true_when_app_not_running()
    {
        Assert.True(AppProcessDetector.TryCloseRunning(
            "com.ribanense.app.inexistente." + Guid.NewGuid().ToString("N"),
            executablePath: null,
            timeout: TimeSpan.FromMilliseconds(200)));
    }
}
