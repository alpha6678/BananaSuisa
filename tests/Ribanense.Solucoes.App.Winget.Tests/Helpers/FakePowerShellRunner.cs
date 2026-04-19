using Ribanense.Solucoes.App.Winget.Services;
using Ribanense.Solucoes.App.Winget.Services.Diagnostics;

namespace Ribanense.Solucoes.App.Winget.Tests.Helpers;

public sealed class FakePowerShellRunner : IPowerShellRunner
{
    public List<string> Commands { get; } = new();
    public Dictionary<string, PowerShellResult> ResponsesByKeyword { get; } = new(StringComparer.OrdinalIgnoreCase);
    public PowerShellResult Default { get; set; } = new PowerShellResult(0, "{}", "");

    public Task<PowerShellResult> RunAsync(string command, CancellationToken ct)
    {
        Commands.Add(command);
        foreach (var kv in ResponsesByKeyword)
        {
            if (command.Contains(kv.Key, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(kv.Value);
            }
        }
        return Task.FromResult(Default);
    }
}

public sealed class FakeWingetLocator : IWingetLocator
{
    public string? Path { get; set; }
    public string? TryLocate() => Path;
}
