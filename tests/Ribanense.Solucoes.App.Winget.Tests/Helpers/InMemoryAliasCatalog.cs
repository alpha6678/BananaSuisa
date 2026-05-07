using Ribanense.Solucoes.App.Winget.Services;
using Ribanense.Solucoes.App.Winget.Services.Search;

namespace Ribanense.Solucoes.App.Winget.Tests.Helpers;

public sealed class InMemoryAliasCatalog : IAppAliasCatalog
{
    public InMemoryAliasCatalog(params AppAlias[] aliases)
    {
        All = aliases;
    }

    public IReadOnlyList<AppAlias> All { get; }
    public IReadOnlyList<AppAlias> Suggested => All
        .Where(a => a.IsSuggested)
        .OrderBy(a => a.SuggestedOrder ?? int.MaxValue)
        .ThenBy(a => a.PublicName ?? a.Id, StringComparer.OrdinalIgnoreCase)
        .ToList();
}

public sealed class FakeWingetSearchService : IWingetSearchService
{
    public Dictionary<string, List<Ribanense.Solucoes.App.Winget.Domain.WingetPackage>> ByQuery { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Calls { get; } = new();

    public Task<IReadOnlyList<Ribanense.Solucoes.App.Winget.Domain.WingetPackage>> SearchAsync(string query, CancellationToken ct)
    {
        Calls.Add(query);
        if (ByQuery.TryGetValue(query, out var pkgs))
        {
            return Task.FromResult<IReadOnlyList<Ribanense.Solucoes.App.Winget.Domain.WingetPackage>>(pkgs);
        }
        return Task.FromResult<IReadOnlyList<Ribanense.Solucoes.App.Winget.Domain.WingetPackage>>(Array.Empty<Ribanense.Solucoes.App.Winget.Domain.WingetPackage>());
    }
}
