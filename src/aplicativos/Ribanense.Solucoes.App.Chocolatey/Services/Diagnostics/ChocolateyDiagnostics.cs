namespace Ribanense.Solucoes.App.Chocolatey.Services.Diagnostics;

public sealed class ChocolateyDiagnostics : IChocolateyDiagnostics
{
    private readonly IChocolateyLocator _locator;
    private readonly IChocolateyExecutor _executor;

    public ChocolateyDiagnostics(IChocolateyLocator locator, IChocolateyExecutor executor)
    {
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public async Task<ChocolateyStatus> InspectAsync(CancellationToken ct)
    {
        string? path = _locator.TryLocate();
        if (path is null)
        {
            return new ChocolateyStatus(
                Found: false,
                Path: null,
                Version: null,
                Error: "choco.exe nao encontrado.");
        }

        try
        {
            var result = await _executor.RunAsync(["--version"], ct: ct).ConfigureAwait(false);
            string version = FirstNonEmptyLine(result.Stdout);
            if (result.Success && !string.IsNullOrWhiteSpace(version))
            {
                return new ChocolateyStatus(true, path, version, null);
            }

            string error = string.IsNullOrWhiteSpace(result.Stderr)
                ? $"Chocolatey retornou codigo {result.ExitCode}."
                : result.Stderr.Trim();
            return new ChocolateyStatus(true, path, null, error);
        }
        catch (Exception ex)
        {
            return new ChocolateyStatus(true, path, null, ex.Message);
        }
    }

    private static string FirstNonEmptyLine(string value)
        => value
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;
}
