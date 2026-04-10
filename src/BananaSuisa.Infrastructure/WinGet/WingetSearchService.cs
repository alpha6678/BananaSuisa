using System.Text.Json;
using BananaSuisa.Core.Winget;
using BananaSuisa.Infrastructure.Provisioning;
using BananaSuisa.Services.Abstractions;

namespace BananaSuisa.Infrastructure.WinGet;

public sealed class WingetSearchService : IWingetSearchService
{
    private const int HardCap = 500;
    private readonly IWingetLocator _locator;

    public WingetSearchService(IWingetLocator locator)
    {
        _locator = locator;
    }

    public async Task<WingetSearchOutcome> SearchAsync(string query, int maxResults, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return WingetSearchOutcome.Fail("Pesquisa winget disponivel apenas no Windows.");
        }

        string trimmed = query.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return WingetSearchOutcome.Fail("Digite um termo para pesquisar (nome, ID ou editor).");
        }

        if (maxResults < 1)
        {
            maxResults = 50;
        }

        maxResults = Math.Min(maxResults, HardCap);

        string? wingetPath = _locator.TryLocate();
        if (string.IsNullOrWhiteSpace(wingetPath))
        {
            return WingetSearchOutcome.Fail("winget.exe nao encontrado. Instale o App Installer ou use a secao Winget para provisionar.");
        }

        string safeQuery = trimmed.Replace("\"", "", StringComparison.Ordinal);
        string args = $"search \"{safeQuery}\" --json --accept-source-agreements";

        ProcessRunResult run = await ProcessRunner.RunAsync(wingetPath, args, cancellationToken).ConfigureAwait(false);

        if (run.ExitCode != 0)
        {
            string err = $"{run.StandardError}{run.StandardOutput}".Trim();
            return WingetSearchOutcome.Fail(string.IsNullOrEmpty(err) ? $"winget search falhou (exit {run.ExitCode})." : err);
        }

        string json = run.StandardOutput.Trim();
        if (string.IsNullOrEmpty(json))
        {
            return WingetSearchOutcome.Ok("Nenhum resultado.", []);
        }

        List<WingetSearchItem> items;
        try
        {
            items = ParseJson(json, maxResults);
        }
        catch (Exception ex)
        {
            return WingetSearchOutcome.Fail($"Nao foi possivel interpretar a saida do winget. Atualize o App Installer/winget. Detalhe: {ex.Message}");
        }

        if (items.Count == 0)
        {
            return WingetSearchOutcome.Ok("Nenhum pacote encontrado para este termo.", []);
        }

        return WingetSearchOutcome.Ok($"{items.Count} resultado(s) (limite {maxResults}).", items);
    }

    private static List<WingetSearchItem> ParseJson(string json, int maxResults)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        List<WingetSearchItem> list = [];

        if (root.TryGetProperty("Sources", out JsonElement sources) && sources.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement source in sources.EnumerateArray())
            {
                string sourceName = GetString(source, "SourceName") ?? GetString(source, "Name") ?? "";

                if (!source.TryGetProperty("Packages", out JsonElement packages) || packages.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (JsonElement pkg in packages.EnumerateArray())
                {
                    if (list.Count >= maxResults)
                    {
                        return list;
                    }

                    string id = GetString(pkg, "PackageIdentifier") ?? GetString(pkg, "Id") ?? "";
                    string name = GetString(pkg, "PackageName") ?? GetString(pkg, "Name") ?? id;
                    string version = ExtractVersion(pkg);
                    list.Add(new WingetSearchItem(name, id, version, sourceName));

                    if (list.Count >= maxResults)
                    {
                        return list;
                    }
                }
            }
        }

        return list;
    }

    private static string ExtractVersion(JsonElement pkg)
    {
        if (pkg.TryGetProperty("Versions", out JsonElement versions) && versions.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement v in versions.EnumerateArray())
            {
                string? ver = GetString(v, "Version");
                if (!string.IsNullOrEmpty(ver))
                {
                    return ver;
                }
            }
        }

        return GetString(pkg, "Version") ?? "";
    }

    private static string? GetString(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out JsonElement p))
        {
            return null;
        }

        return p.ValueKind switch
        {
            JsonValueKind.String => p.GetString(),
            JsonValueKind.Number => p.GetRawText(),
            _ => null
        };
    }

}
