using Ribanense.Solucoes.App.Scripts.Scripts.Commands;
using Xunit;

namespace Ribanense.Solucoes.App.Scripts.Tests;

public class CommandSequenceRunnerTests
{
    [Fact]
    public async Task RunSequenceAsync_runs_non_elevated_command_and_captures_output()
    {
        var runner = new CommandSequenceRunner();
        var lines = new List<string>();
        var progress = new Progress<string>(lines.Add);

        var step = new ShellCommandStep(
            Description: "Echo de teste",
            Executable: "cmd.exe",
            Arguments: new[] { "/c", "echo ola-scripts" });

        var results = await runner.RunSequenceAsync(new[] { step }, progress, CancellationToken.None);

        Assert.Single(results);
        Assert.True(results[0].Succeeded);
        Assert.Contains(lines, l => l.Contains("ola-scripts"));
    }

    [Fact]
    public async Task RunSequenceAsync_stops_on_error_when_requested()
    {
        var runner = new CommandSequenceRunner();

        var failingStep = new ShellCommandStep("Falha proposital", "cmd.exe", new[] { "/c", "exit 1" });
        var neverRunStep = new ShellCommandStep("Nunca deve rodar", "cmd.exe", new[] { "/c", "echo nao-deveria-rodar" });

        var results = await runner.RunSequenceAsync(
            new[] { failingStep, neverRunStep }, onLine: null, CancellationToken.None, stopOnError: true);

        Assert.Single(results);
        Assert.Equal(1, results[0].ExitCode);
        Assert.False(results[0].Succeeded);
    }

    [Fact]
    public void ToCommandText_quotes_arguments_containing_spaces()
    {
        var step = new ShellCommandStep("desc", "powershell.exe", new[] { "-Command", "Write-Host hello world" });

        string text = step.ToCommandText();

        Assert.Equal("powershell.exe -Command \"Write-Host hello world\"", text);
    }
}
