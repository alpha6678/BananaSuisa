using Ribanense.Solucoes.App.Winget.Services.Diagnostics;

namespace Ribanense.Solucoes.App.Winget.Tests.Helpers;

public sealed class FakeElevatedCommandRunner : IElevatedCommandRunner
{
    public List<string> Scripts { get; } = new();

    public ElevatedResult ForcedResult { get; set; } = new ElevatedResult(0, "ok", Cancelled: false);

    public Task<ElevatedResult> RunScriptAsync(string powerShellScript, IProgress<string>? onLine, CancellationToken ct)
    {
        Scripts.Add(powerShellScript);
        if (!string.IsNullOrEmpty(ForcedResult.Output) && onLine is not null)
        {
            foreach (var line in ForcedResult.Output.Split('\n'))
            {
                onLine.Report(line.TrimEnd('\r'));
            }
        }
        return Task.FromResult(ForcedResult);
    }
}
