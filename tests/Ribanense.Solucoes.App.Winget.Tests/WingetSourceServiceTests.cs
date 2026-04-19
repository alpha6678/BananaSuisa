using Ribanense.Solucoes.App.Winget.Domain;
using Ribanense.Solucoes.App.Winget.Services.Diagnostics;
using Ribanense.Solucoes.App.Winget.Services.Sources;
using Ribanense.Solucoes.App.Winget.Tests.Helpers;
using Xunit;

namespace Ribanense.Solucoes.App.Winget.Tests;

public class WingetSourceServiceTests
{
    private const string SampleListOutput = """
Name     Argument                                             Type
----     --------                                             ----
msstore  https://storeedgefd.dsx.mp.microsoft.com/v9.0        Microsoft.Rest
winget   https://cdn.winget.microsoft.com/cache               Microsoft.PreIndexed.Package
""";

    [Fact]
    public async Task ListAsync_parses_rows()
    {
        var exec = new FakeWingetExecutor
        {
            ForcedResponse = new WingetRunResult(0, SampleListOutput, "")
        };
        var svc = new WingetSourceService(exec, new FakeElevatedCommandRunner());

        var sources = await svc.ListAsync(CancellationToken.None);

        Assert.Equal(2, sources.Count);
        Assert.Equal("msstore", sources[0].Name);
        Assert.Equal("Microsoft.Rest", sources[0].Type);
        Assert.Equal("winget", sources[1].Name);
        Assert.Contains("cdn.winget", sources[1].Argument);
    }

    [Fact]
    public async Task ListAsync_uses_source_list_args()
    {
        var exec = new FakeWingetExecutor
        {
            ForcedResponse = new WingetRunResult(0, SampleListOutput, "")
        };
        var svc = new WingetSourceService(exec, new FakeElevatedCommandRunner());
        await svc.ListAsync(CancellationToken.None);

        var args = exec.Calls[0];
        Assert.Equal("source", args[0]);
        Assert.Equal("list", args[1]);
        Assert.Contains("--disable-interactivity", args);
    }

    [Fact]
    public async Task UpdateAsync_without_name_does_not_include_name_flag()
    {
        var exec = new FakeWingetExecutor();
        var svc = new WingetSourceService(exec, new FakeElevatedCommandRunner());

        await svc.UpdateAsync(null, null, CancellationToken.None);

        var args = exec.Calls[0];
        Assert.Equal("source", args[0]);
        Assert.Equal("update", args[1]);
        Assert.DoesNotContain("--name", args);
    }

    [Fact]
    public async Task UpdateAsync_with_name_passes_name()
    {
        var exec = new FakeWingetExecutor();
        var svc = new WingetSourceService(exec, new FakeElevatedCommandRunner());

        await svc.UpdateAsync("winget", null, CancellationToken.None);

        var args = exec.Calls[0];
        Assert.Contains("--name", args);
        Assert.Contains("winget", args);
    }

    [Fact]
    public async Task AddAsync_passes_name_arg_type()
    {
        var exec = new FakeWingetExecutor();
        var svc = new WingetSourceService(exec, new FakeElevatedCommandRunner());

        await svc.AddAsync("custom", "https://example.com", "Microsoft.PreIndexed.Package", null, CancellationToken.None);

        var args = exec.Calls[0];
        Assert.Equal("add", args[1]);
        Assert.Contains("--name", args);
        Assert.Contains("custom", args);
        Assert.Contains("--arg", args);
        Assert.Contains("https://example.com", args);
        Assert.Contains("--type", args);
        Assert.Contains("Microsoft.PreIndexed.Package", args);
    }

    [Fact]
    public async Task AddAsync_empty_args_throw()
    {
        var svc = new WingetSourceService(new FakeWingetExecutor(), new FakeElevatedCommandRunner());
        await Assert.ThrowsAsync<ArgumentException>(() => svc.AddAsync("", "y", "z", null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => svc.AddAsync("x", "", "z", null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => svc.AddAsync("x", "y", "", null, CancellationToken.None));
    }

    [Fact]
    public async Task RemoveAsync_uses_remove_verb_and_name()
    {
        var exec = new FakeWingetExecutor();
        var svc = new WingetSourceService(exec, new FakeElevatedCommandRunner());

        await svc.RemoveAsync("custom", null, CancellationToken.None);

        var args = exec.Calls[0];
        Assert.Equal("remove", args[1]);
        Assert.Contains("--name", args);
        Assert.Contains("custom", args);
    }

    [Fact]
    public async Task ResetAsync_delegates_to_ElevatedCommandRunner()
    {
        var exec = new FakeWingetExecutor();
        var elev = new FakeElevatedCommandRunner
        {
            ForcedResult = new ElevatedResult(0, "Fontes redefinidas.", Cancelled: false)
        };
        var svc = new WingetSourceService(exec, elev);

        var result = await svc.ResetAsync(null, CancellationToken.None);

        // Nao deve chamar o executor; todo o trabalho vai pro runner elevado.
        Assert.Empty(exec.Calls);
        Assert.Single(elev.Scripts);
        Assert.Contains("winget source reset --force", elev.Scripts[0]);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task ResetAsync_when_uac_cancelled_reports_cancellation_on_stderr()
    {
        var elev = new FakeElevatedCommandRunner
        {
            ForcedResult = new ElevatedResult(ElevatedCommandRunner.UacCancelledExitCode, "", Cancelled: true)
        };
        var svc = new WingetSourceService(new FakeWingetExecutor(), elev);

        var result = await svc.ResetAsync(null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(1223, result.ExitCode);
        Assert.Contains("cancelada", result.Stderr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportAsync_returns_stdout()
    {
        var exec = new FakeWingetExecutor
        {
            ForcedResponse = new WingetRunResult(0, "{\"Sources\": []}", "")
        };
        var svc = new WingetSourceService(exec, new FakeElevatedCommandRunner());

        string json = await svc.ExportAsync(CancellationToken.None);

        Assert.Contains("Sources", json);
    }
}
