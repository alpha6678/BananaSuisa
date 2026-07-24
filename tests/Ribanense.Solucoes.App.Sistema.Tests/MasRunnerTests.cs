using System.Diagnostics;
using System.Net.Http;
using Ribanense.Solucoes.App.Sistema.Services;
using Xunit;

namespace Ribanense.Solucoes.App.Sistema.Tests;

public class MasRunnerTests
{
    [Fact]
    public async Task RunAsync_returns_failure_when_download_throws_and_no_file_exists()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), "ribanense-sistema-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var runner = new MasRunner(cacheDir, new FakeLauncher(), () => new HttpClient(new ThrowingHandler()));

            var result = await runner.RunAsync(MasMethod.Hwid, null, CancellationToken.None);

            Assert.False(result.Success);
            Assert.False(result.Cancelled);
            Assert.NotNull(result.Error);
        }
        finally
        {
            TryDeleteDir(cacheDir);
        }
    }

    [Fact]
    public void GetScriptInfo_returns_false_when_file_does_not_exist()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), "ribanense-sistema-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var runner = new MasRunner(cacheDir, new FakeLauncher());
            var info = runner.GetScriptInfo();

            Assert.False(info.Exists);
            Assert.Null(info.LastDownloaded);
            Assert.EndsWith("MAS_AIO.cmd", info.FilePath);
        }
        finally
        {
            TryDeleteDir(cacheDir);
        }
    }

    [Fact]
    public async Task GetScriptInfo_returns_true_after_script_is_created()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), "ribanense-sistema-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(cacheDir);
            string scriptPath = Path.Combine(cacheDir, "MAS_AIO.cmd");
            await File.WriteAllTextAsync(scriptPath, "echo test");

            var runner = new MasRunner(cacheDir, new FakeLauncher());
            var info = runner.GetScriptInfo();

            Assert.True(info.Exists);
            Assert.NotNull(info.LastDownloaded);
        }
        finally
        {
            TryDeleteDir(cacheDir);
        }
    }

    [Fact]
    public async Task RunAsync_uses_cmd_k_switch_for_interactive_cmd_mode()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), "ribanense-sistema-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(cacheDir);
            string scriptPath = Path.Combine(cacheDir, "MAS_AIO.cmd");
            await File.WriteAllTextAsync(scriptPath, "echo test");

            var fakeLauncher = new RecordingLauncher();
            var runner = new MasRunner(cacheDir, fakeLauncher);

            var options = new MasRunOptions(InteractiveTerminal: true, ForceRedownload: false, Engine: MasEngine.Cmd);
            var result = await runner.RunAsync(MasMethod.Hwid, options, null, CancellationToken.None);

            Assert.True(result.Success);
            Assert.NotNull(fakeLauncher.LastInfo);
            Assert.Equal("cmd.exe", fakeLauncher.LastInfo.FileName);
            Assert.Contains("/k", fakeLauncher.LastInfo.ArgumentList);
            Assert.Equal("runas", fakeLauncher.LastInfo.Verb);
        }
        finally
        {
            TryDeleteDir(cacheDir);
        }
    }

    [Fact]
    public async Task RunAsync_uses_powershell_noexit_for_interactive_powershell_mode()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), "ribanense-sistema-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(cacheDir);
            string scriptPath = Path.Combine(cacheDir, "MAS_AIO.cmd");
            await File.WriteAllTextAsync(scriptPath, "echo test");

            var fakeLauncher = new RecordingLauncher();
            var runner = new MasRunner(cacheDir, fakeLauncher);

            var options = new MasRunOptions(InteractiveTerminal: true, ForceRedownload: false, Engine: MasEngine.PowerShell);
            var result = await runner.RunAsync(MasMethod.Ohook, options, null, CancellationToken.None);

            Assert.True(result.Success);
            Assert.NotNull(fakeLauncher.LastInfo);
            Assert.Equal("powershell.exe", fakeLauncher.LastInfo.FileName);
            Assert.Contains("-NoExit", fakeLauncher.LastInfo.ArgumentList);
            Assert.Equal("runas", fakeLauncher.LastInfo.Verb);
        }
        finally
        {
            TryDeleteDir(cacheDir);
        }
    }

    private static void TryDeleteDir(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("simulado");
    }

    private sealed class FakeLauncher : IProcessLauncher
    {
        public Task<int> StartAndWaitAsync(ProcessStartInfo info, CancellationToken ct)
            => throw new InvalidOperationException("nao deveria chegar aqui");
    }

    private sealed class RecordingLauncher : IProcessLauncher
    {
        public ProcessStartInfo? LastInfo { get; private set; }

        public Task<int> StartAndWaitAsync(ProcessStartInfo info, CancellationToken ct)
        {
            LastInfo = info;
            return Task.FromResult(0);
        }
    }
}
