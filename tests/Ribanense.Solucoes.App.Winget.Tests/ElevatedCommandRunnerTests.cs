using Ribanense.Solucoes.App.Winget.Services.Diagnostics;
using Ribanense.Solucoes.App.Winget.Tests.Helpers;
using Xunit;

namespace Ribanense.Solucoes.App.Winget.Tests;

public class ElevatedCommandRunnerTests
{
    [Fact]
    public async Task RunScriptAsync_writes_script_file_and_returns_exit_code()
    {
        var launcher = new FakeProcessLauncher
        {
            ExitCode = 0,
            LogToWrite = "linha1\r\nlinha2"
        };
        var runner = new ElevatedCommandRunner(launcher, () => "test001");

        var progress = new List<string>();
        var prog = new Progress<string>(line => progress.Add(line));

        var result = await runner.RunScriptAsync("Write-Host 'ola'", prog, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.False(result.Cancelled);

        // O script temporario deve ter sido deletado ao final.
        Assert.NotNull(launcher.ScriptPathCapture);
        Assert.False(System.IO.File.Exists(launcher.ScriptPathCapture!));

        // O processo recebeu powershell.exe com -Verb runas
        Assert.Single(launcher.Started);
        var psi = launcher.Started[0];
        Assert.Equal("runas", psi.Verb);
        Assert.True(psi.UseShellExecute);
        Assert.Contains("-ExecutionPolicy", psi.ArgumentList);
        Assert.Contains("Bypass", psi.ArgumentList);

        // Progress recebeu linhas do log.
        Assert.Contains("linha1", progress);
        Assert.Contains("linha2", progress);

        // Output agregado tambem.
        Assert.Contains("linha1", result.Output);
    }

    [Fact]
    public async Task RunScriptAsync_uac_cancelled_returns_Cancelled_true()
    {
        var launcher = new FakeProcessLauncher
        {
            SimulateUacCancelled = true
        };
        var runner = new ElevatedCommandRunner(launcher, () => "test002");

        var result = await runner.RunScriptAsync("Write-Host 'x'", null, CancellationToken.None);

        Assert.True(result.Cancelled);
        Assert.Equal(ElevatedCommandRunner.UacCancelledExitCode, result.ExitCode);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task RunScriptAsync_nonzero_exit_code_is_failure()
    {
        var launcher = new FakeProcessLauncher { ExitCode = 1, LogToWrite = "erro" };
        var runner = new ElevatedCommandRunner(launcher, () => "test003");

        var result = await runner.RunScriptAsync("Write-Host 'y'", null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("erro", result.Output);
    }

    [Fact]
    public async Task RunScriptAsync_empty_script_throws()
    {
        var runner = new ElevatedCommandRunner(new FakeProcessLauncher());
        await Assert.ThrowsAsync<ArgumentException>(() =>
            runner.RunScriptAsync("", null, CancellationToken.None));
    }

    [Fact]
    public void BuildWrapper_includes_start_transcript_and_user_script()
    {
        string wrapper = ElevatedCommandRunner.BuildWrapper(
            "Write-Host 'ola mundo'",
            "C:\\tmp\\log.log");

        Assert.Contains("Start-Transcript", wrapper);
        Assert.Contains("Stop-Transcript", wrapper);
        Assert.Contains("Write-Host 'ola mundo'", wrapper);
        Assert.Contains("C:\\tmp\\log.log", wrapper);
        Assert.Contains("exit $exit", wrapper);
    }

    [Fact]
    public void BuildWrapper_escapes_single_quotes_in_log_path()
    {
        string wrapper = ElevatedCommandRunner.BuildWrapper(
            "Write-Host 'x'",
            "C:\\user's\\path\\log.log");

        // aspas simples duplicadas dentro da string PowerShell
        Assert.Contains("user''s", wrapper);
    }
}
