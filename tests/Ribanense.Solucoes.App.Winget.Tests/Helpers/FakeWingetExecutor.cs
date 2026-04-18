using Ribanense.Solucoes.App.Winget.Domain;
using Ribanense.Solucoes.App.Winget.Services;

namespace Ribanense.Solucoes.App.Winget.Tests.Helpers;

public sealed class FakeWingetExecutor : IWingetExecutor
{
    public List<List<string>> Calls { get; } = new();

    public Func<IReadOnlyList<string>, WingetRunResult> ResponseProvider { get; set; } =
        _ => new WingetRunResult(0, string.Empty, string.Empty);

    public WingetRunResult? ForcedResponse { get; set; }

    public Task<WingetRunResult> RunAsync(
        IEnumerable<string> args,
        Action<string>? onStdout = null,
        Action<string>? onStderr = null,
        CancellationToken ct = default)
    {
        var list = args.ToList();
        Calls.Add(list);

        var response = ForcedResponse ?? ResponseProvider(list);

        foreach (string line in (response.Stdout ?? string.Empty).Split('\n'))
        {
            if (!string.IsNullOrEmpty(line))
                onStdout?.Invoke(line.TrimEnd('\r'));
        }
        foreach (string line in (response.Stderr ?? string.Empty).Split('\n'))
        {
            if (!string.IsNullOrEmpty(line))
                onStderr?.Invoke(line.TrimEnd('\r'));
        }

        return Task.FromResult(response);
    }
}
