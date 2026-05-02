using Ribanense.Solucoes.App.Chocolatey.Domain;
using Ribanense.Solucoes.App.Chocolatey.Services;

namespace Ribanense.Solucoes.App.Chocolatey.Tests.Helpers;

public sealed class FakeChocolateyExecutor : IChocolateyExecutor
{
    private readonly Queue<ChocolateyRunResult> _results = new();

    public List<IReadOnlyList<string>> Calls { get; } = new();

    public FakeChocolateyExecutor Enqueue(int exitCode, string stdout = "", string stderr = "")
    {
        _results.Enqueue(new ChocolateyRunResult(exitCode, stdout, stderr));
        return this;
    }

    public Task<ChocolateyRunResult> RunAsync(
        IEnumerable<string> args,
        Action<string>? onStdout = null,
        Action<string>? onStderr = null,
        bool requireAdmin = false,
        CancellationToken ct = default)
    {
        Calls.Add(args.ToArray());
        var result = _results.Count > 0
            ? _results.Dequeue()
            : new ChocolateyRunResult(0, string.Empty, string.Empty);

        foreach (string line in result.Stdout.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            onStdout?.Invoke(line);
        }
        foreach (string line in result.Stderr.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            onStderr?.Invoke(line);
        }

        return Task.FromResult(result);
    }
}
