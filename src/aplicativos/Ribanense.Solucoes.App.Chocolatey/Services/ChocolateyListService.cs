using Ribanense.Solucoes.App.Chocolatey.Domain;

namespace Ribanense.Solucoes.App.Chocolatey.Services;

public sealed class ChocolateyListService : IChocolateyListService
{
    private readonly IChocolateyExecutor _executor;

    public ChocolateyListService(IChocolateyExecutor executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public async Task<IReadOnlyList<InstalledChocolateyPackage>> GetInstalledAsync(CancellationToken ct)
    {
        var installedTask = _executor.RunAsync(
            ["list", "--local-only", "--limit-output"],
            ct: ct);
        var outdatedTask = _executor.RunAsync(
            ["outdated", "--limit-output"],
            ct: ct);

        await Task.WhenAll(installedTask, outdatedTask).ConfigureAwait(false);

        var updates = ParseOutdatedOutput(outdatedTask.Result.Stdout);
        return ParseListOutput(installedTask.Result.Stdout, updates);
    }

    internal static IReadOnlyList<InstalledChocolateyPackage> ParseListOutput(
        string stdout,
        IReadOnlyDictionary<string, string>? availableVersions = null)
    {
        var rows = ChocolateyLimitedOutputParser.ParsePipeRows(stdout, minimumColumns: 2);
        var list = new List<InstalledChocolateyPackage>(rows.Count);

        foreach (string[] row in rows)
        {
            string id = row[0];
            string installed = row[1];
            string? available = null;
            availableVersions?.TryGetValue(id, out available);

            list.Add(new InstalledChocolateyPackage(
                Name: id,
                Id: id,
                InstalledVersion: installed,
                AvailableVersion: available,
                Source: "local"));
        }

        return list;
    }

    internal static IReadOnlyDictionary<string, string> ParseOutdatedOutput(string stdout)
    {
        var rows = ChocolateyLimitedOutputParser.ParsePipeRows(stdout, minimumColumns: 3);
        var updates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string[] row in rows)
        {
            string id = row[0];
            string available = row[2];
            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(available))
            {
                updates[id] = available;
            }
        }

        return updates;
    }
}
