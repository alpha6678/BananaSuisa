using Ribanense.Solucoes.App.Sistema.Services;
using Ribanense.Solucoes.App.Sistema.ViewModels;
using Xunit;

namespace Ribanense.Solucoes.App.Sistema.Tests;

public class ActivationViewModelTests
{
    [Fact]
    public void Initial_state_defaults_to_interactive_mode_and_cmd_engine()
    {
        var runner = new FakeRunner();
        var vm = new ActivationViewModel(runner);

        Assert.True(vm.IsInteractiveMode);
        Assert.False(vm.IsDirectMode);
        Assert.True(vm.UseCmd);
        Assert.False(vm.UsePowerShell);
        Assert.NotNull(vm.ScriptStatusText);
        Assert.NotEmpty(vm.Methods);
    }

    [Fact]
    public void Toggling_modes_updates_related_properties()
    {
        var runner = new FakeRunner();
        var vm = new ActivationViewModel(runner);

        vm.IsDirectMode = true;
        Assert.False(vm.IsInteractiveMode);
        Assert.True(vm.IsDirectMode);

        vm.UsePowerShell = true;
        Assert.True(vm.UsePowerShell);
        Assert.False(vm.UseCmd);
    }

    [Fact]
    public async Task RunCommand_executes_method_and_invokes_runner()
    {
        var runner = new FakeRunner();
        var vm = new ActivationViewModel(runner);

        var loggedLines = new List<string>();
        vm.AttachUiLog(line => loggedLines.Add(line));

        vm.RunCommand.Execute(MasMethod.Hwid);

        // Allow async execution to process
        await Task.Delay(50);

        Assert.NotNull(runner.LastRunMethod);
        Assert.Equal("hwid", runner.LastRunMethod.Id);
        Assert.NotEmpty(loggedLines);
        Assert.Contains(loggedLines, l => l.Contains("Iniciando: HWID"));
    }

    [Fact]
    public async Task UpdateScriptCommand_triggers_redownload_and_refreshes_status()
    {
        var runner = new FakeRunner();
        var vm = new ActivationViewModel(runner);

        var loggedLines = new List<string>();
        vm.AttachUiLog(line => loggedLines.Add(line));

        vm.UpdateScriptCommand.Execute(null);

        await Task.Delay(50);

        Assert.True(runner.RedownloadCalled);
        Assert.Contains(loggedLines, l => l.Contains("Solicitando atualização do script MAS"));
    }

    [Fact]
    public async Task OpenInteractiveMenuCommand_launches_troubleshoot_interactively()
    {
        var runner = new FakeRunner();
        var vm = new ActivationViewModel(runner);

        var loggedLines = new List<string>();
        vm.AttachUiLog(line => loggedLines.Add(line));

        vm.OpenInteractiveMenuCommand.Execute(null);

        await Task.Delay(50);

        Assert.NotNull(runner.LastRunMethod);
        Assert.Equal("troubleshoot", runner.LastRunMethod.Id);
        Assert.NotNull(runner.LastRunOptions);
        Assert.True(runner.LastRunOptions.InteractiveTerminal);
    }

    private sealed class FakeRunner : IMasRunner
    {
        public MasMethod? LastRunMethod { get; private set; }
        public MasRunOptions? LastRunOptions { get; private set; }
        public bool RedownloadCalled { get; private set; }

        public MasScriptInfo GetScriptInfo()
            => new MasScriptInfo(true, DateTime.Now, "C:\\Fake\\MAS_AIO.cmd");

        public Task<bool> RedownloadScriptAsync(IProgress<string>? onLine, CancellationToken ct)
        {
            RedownloadCalled = true;
            onLine?.Report("Script baixado fake.");
            return Task.FromResult(true);
        }

        public Task<MasRunResult> RunAsync(MasMethod method, IProgress<string>? onLine, CancellationToken ct)
            => RunAsync(method, new MasRunOptions(), onLine, ct);

        public Task<MasRunResult> RunAsync(MasMethod method, MasRunOptions? options, IProgress<string>? onLine, CancellationToken ct)
        {
            LastRunMethod = method;
            LastRunOptions = options;
            onLine?.Report($"Executando {method.Display} fake...");
            return Task.FromResult(new MasRunResult(true, null, false));
        }
    }
}
