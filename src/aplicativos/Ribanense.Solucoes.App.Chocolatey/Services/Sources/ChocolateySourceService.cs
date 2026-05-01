using Ribanense.Solucoes.App.Chocolatey.Domain;

namespace Ribanense.Solucoes.App.Chocolatey.Services.Sources;

public sealed class ChocolateySourceService : IChocolateySourceService
{
    private readonly IChocolateyExecutor _executor;

    public ChocolateySourceService(IChocolateyExecutor executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public async Task<IReadOnlyList<ChocolateySource>> ListAsync(CancellationToken ct)
    {
        var result = await _executor.RunAsync(["source", "list", "--limit-output"], ct: ct)
            .ConfigureAwait(false);
        return ParseListOutput(result.Stdout);
    }

    public Task<ChocolateyRunResult> RemoveAsync(string name, Action<string>? onLine, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name obrigatório.", nameof(name));

        var args = new[]
        {
            "source",
            "remove",
            "--name",
            name
        };

        return _executor.RunAsync(args, onStdout: onLine, onStderr: onLine, ct: ct);
    }

    internal static IReadOnlyList<ChocolateySource> ParseListOutput(string stdout)
    {
        var rows = ChocolateyLimitedOutputParser.ParsePipeRows(stdout, minimumColumns: 2);
        var sources = new List<ChocolateySource>(rows.Count);

        foreach (string[] row in rows)
        {
            bool disabled = row.Length > 2 && bool.TryParse(row[2], out bool parsed) && parsed;
            string priority = row.Length > 3 ? row[3] : string.Empty;

            sources.Add(new ChocolateySource(
                Name: row[0],
                Url: row[1],
                Disabled: disabled,
                Priority: priority));
        }

        return sources;
    }
}
