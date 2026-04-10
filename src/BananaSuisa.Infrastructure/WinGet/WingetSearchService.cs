using System.Text.Json;
using System.Text.RegularExpressions;
using BananaSuisa.Core.Winget;
using BananaSuisa.Infrastructure.Provisioning;
using BananaSuisa.Services.Abstractions;

namespace BananaSuisa.Infrastructure.WinGet;

public sealed class WingetSearchService : IWingetSearchService
{
    private const int HardCap = 500;
    private const int MaxPackagesBeforeRank = 500;
    private const int FailureDetailMaxChars = 8000;
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

        string cliQuery = WingetSearchRelevance.BuildWingetCliQuery(trimmed);
        string safeQuery = cliQuery.Replace("\"", "", StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(safeQuery))
        {
            safeQuery = trimmed.Replace("\"", "", StringComparison.Ordinal);
        }

        // 1) tentar JSON (winget recente)
        string argsJson = $"search \"{safeQuery}\" --accept-source-agreements --json";
        ProcessRunResult runJson = await ProcessRunner.RunAsync(wingetPath, argsJson, cancellationToken).ConfigureAwait(false);

        if (runJson.ExitCode == 0)
        {
            string json = runJson.StandardOutput.Trim();
            if (json.StartsWith("{", StringComparison.Ordinal))
            {
                try
                {
                    List<WingetSearchItem> items = ParseJson(json, MaxPackagesBeforeRank);
                    if (items.Count == 0)
                    {
                        return WingetSearchOutcome.Ok("Nenhum pacote encontrado para este termo.", []);
                    }

                    IReadOnlyList<WingetSearchItem> ranked = WingetSearchRelevance.RankByRelevance(items, trimmed, maxResults);
                    return WingetSearchOutcome.Ok(
                        $"{ranked.Count} resultado(s) por similaridade ao texto (limite {maxResults}).",
                        ranked);
                }
                catch (Exception ex)
                {
                    return WingetSearchOutcome.Fail(
                        $"Nao foi possivel interpretar a saida JSON do winget. Detalhe: {ex.Message}",
                        TruncateCombined(runJson));
                }
            }
        }

        // 2) fallback: saida em texto (winget antigo ou --json nao suportado)
        string argsText = $"search \"{safeQuery}\" --accept-source-agreements";
        ProcessRunResult runText = await ProcessRunner.RunAsync(wingetPath, argsText, cancellationToken).ConfigureAwait(false);

        if (runText.ExitCode != 0)
        {
            return FailFromProcess(runText, "Pesquisa winget falhou");
        }

        List<WingetSearchItem> textItems = ParseTextOutput(runText.StandardOutput, MaxPackagesBeforeRank);
        if (textItems.Count == 0)
        {
            return WingetSearchOutcome.Ok("Nenhum pacote encontrado para este termo.", []);
        }

        string note = runJson.ExitCode != 0 || !runJson.StandardOutput.TrimStart().StartsWith("{", StringComparison.Ordinal)
            ? " (saida em texto; atualize o App Installer para suportar --json)"
            : "";

        IReadOnlyList<WingetSearchItem> rankedText = WingetSearchRelevance.RankByRelevance(textItems, trimmed, maxResults);
        return WingetSearchOutcome.Ok(
            $"{rankedText.Count} resultado(s) por similaridade ao texto (limite {maxResults}){note}.",
            rankedText);
    }

    public async Task<WingetSearchOutcome> ListInstalledAsync(int maxResults, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return WingetSearchOutcome.Fail("Listagem winget disponivel apenas no Windows.");
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

        string argsJson = "list --accept-source-agreements --json";
        ProcessRunResult runJson = await ProcessRunner.RunAsync(wingetPath, argsJson, cancellationToken).ConfigureAwait(false);

        if (runJson.ExitCode == 0)
        {
            string json = runJson.StandardOutput.Trim();
            if (json.StartsWith("{", StringComparison.Ordinal))
            {
                try
                {
                    List<WingetSearchItem> items = ParseJson(json, maxResults);
                    return WingetSearchOutcome.Ok(
                        items.Count == 0
                            ? "Nenhum pacote winget listado como instalado."
                            : $"{items.Count} pacote(s) instalado(s) (limite {maxResults}).",
                        items);
                }
                catch (Exception ex)
                {
                    return WingetSearchOutcome.Fail(
                        $"Nao foi possivel interpretar a saida JSON do winget list. Detalhe: {ex.Message}",
                        TruncateCombined(runJson));
                }
            }
        }

        string argsText = "list --accept-source-agreements";
        ProcessRunResult runText = await ProcessRunner.RunAsync(wingetPath, argsText, cancellationToken).ConfigureAwait(false);

        if (runText.ExitCode != 0)
        {
            return FailFromProcess(runText, "Listagem winget falhou");
        }

        List<WingetSearchItem> textItems = ParseTextOutput(runText.StandardOutput, maxResults);
        string note = runJson.ExitCode != 0 || !runJson.StandardOutput.TrimStart().StartsWith("{", StringComparison.Ordinal)
            ? " (saida em texto; atualize o App Installer para suportar --json)"
            : "";

        return WingetSearchOutcome.Ok(
            textItems.Count == 0
                ? $"Nenhum pacote winget listado como instalado{note}."
                : $"{textItems.Count} pacote(s) instalado(s) (limite {maxResults}){note}.",
            textItems);
    }

    private static WingetSearchOutcome FailFromProcess(ProcessRunResult run, string shortPrefix)
    {
        string detail = TruncateCombined(run);
        string? userLine = ExtractFirstLineForUser(detail);
        string user = string.IsNullOrEmpty(userLine)
            ? $"{shortPrefix} (exit {run.ExitCode})."
            : userLine;

        if (user.Length > 400)
        {
            user = user.Substring(0, 397) + "...";
        }

        return WingetSearchOutcome.Fail(user, string.IsNullOrEmpty(detail) ? null : detail);
    }

    private static string TruncateCombined(ProcessRunResult run)
    {
        string s = $"{run.StandardError}{run.StandardOutput}".Trim();
        if (s.Length <= FailureDetailMaxChars)
        {
            return s;
        }

        return s.Substring(0, FailureDetailMaxChars) + "\n...[truncado]";
    }

    private static string? ExtractFirstLineForUser(string combined)
    {
        foreach (string line in combined.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string t = line.Trim();
            if (t.Length == 0)
            {
                continue;
            }

            if (t.StartsWith("---", StringComparison.Ordinal))
            {
                continue;
            }

            if (t.StartsWith("Windows Package Manager", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return t;
        }

        return null;
    }

    /// <summary>
    /// Tabela de texto padrao do winget (locale PT/EN): colunas alinhadas; fallback para split legado.
    /// </summary>
    private static List<WingetSearchItem> ParseTextOutput(string text, int maxPackagesBeforeRank)
    {
        List<WingetSearchItem> fromTable = WingetCliTextTableParser.TryParsePackageTable(text, maxPackagesBeforeRank);
        if (fromTable.Count > 0)
        {
            return fromTable;
        }

        return ParseTextOutputLegacySplit(text, maxPackagesBeforeRank);
    }

    /// <summary>
    /// Fallback quando o cabecalho nao e reconhecido: split por espacos (pode falhar se colunas vazias).
    /// </summary>
    private static List<WingetSearchItem> ParseTextOutputLegacySplit(string text, int maxPackagesBeforeRank)
    {
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
        var list = new List<WingetSearchItem>();
        bool pastSeparator = false;

        foreach (string line in lines)
        {
            string t = line.TrimEnd();
            if (string.IsNullOrWhiteSpace(t))
            {
                continue;
            }

            if (IsSeparatorLine(t))
            {
                pastSeparator = true;
                continue;
            }

            if (!pastSeparator)
            {
                continue;
            }

            if (t.StartsWith("Nenhum pacote encontrado", StringComparison.OrdinalIgnoreCase) ||
                t.StartsWith("No package found", StringComparison.OrdinalIgnoreCase) ||
                t.StartsWith("No packages found", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                t.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string[] parts = Regex.Split(t.Trim(), @"\s{2,}")
                .Where(p => p.Length > 0)
                .ToArray();

            if (parts.Length < 2)
            {
                continue;
            }

            string name = parts[0];
            string id = parts[1];
            string version = parts.Length > 2 ? parts[2] : "";
            string source = parts.Length > 3 ? parts[^1] : "";

            // Coluna ID vazia: nome | versao | winget (3 tokens) — sem isto, versao aparece como ID.
            if (parts.Length == 3 && IsWingetRepositoryToken(parts[2]) && LooksLikeVersionToken(parts[1]))
            {
                id = "";
                version = parts[1];
                source = parts[2];
            }

            string origin = WingetInstallationOrigin.Resolve(source, id);

            list.Add(new WingetSearchItem(name, id, version, source, origin));
            if (list.Count >= maxPackagesBeforeRank)
            {
                break;
            }
        }

        return list;
    }

    private static bool IsWingetRepositoryToken(string s)
    {
        return s.Equals("winget", StringComparison.OrdinalIgnoreCase) ||
               s.Equals("msstore", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeVersionToken(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return false;
        }

        // Versoes numericas ou Unknown
        if (s.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return char.IsDigit(s[0]) || (s[0] == '<' && s.Length > 1 && char.IsDigit(s[1]));
    }

    private static bool IsSeparatorLine(string line)
    {
        string trimmed = line.Trim();
        if (trimmed.Length < 5)
        {
            return false;
        }

        return trimmed.All(c => c == '-' || c == '+' || c == ' ' || c == '|');
    }

    private static List<WingetSearchItem> ParseJson(string json, int maxPackagesBeforeRank)
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
                    if (list.Count >= maxPackagesBeforeRank)
                    {
                        return list;
                    }

                    string id = GetString(pkg, "PackageIdentifier") ?? GetString(pkg, "Id") ?? "";
                    string name = GetString(pkg, "PackageName") ?? GetString(pkg, "Name") ?? id;
                    string version = ExtractVersion(pkg);
                    string origin = BuildOriginFromPackageJson(pkg, sourceName, id);
                    list.Add(new WingetSearchItem(name, id, version, sourceName, origin));

                    if (list.Count >= maxPackagesBeforeRank)
                    {
                        return list;
                    }
                }
            }
        }

        return list;
    }

    private static string BuildOriginFromPackageJson(JsonElement pkg, string sourceName, string id)
    {
        string? pfn = GetString(pkg, "PackageFamilyName");
        string? installerType = GetString(pkg, "InstallerType");
        if (string.IsNullOrEmpty(installerType) &&
            pkg.TryGetProperty("Installer", out JsonElement inst) &&
            inst.ValueKind == JsonValueKind.Object)
        {
            installerType = GetString(inst, "InstallerType");
        }

        return WingetInstallationOrigin.Resolve(sourceName, id, pfn, installerType);
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
