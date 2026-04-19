using Ribanense.Solucoes.App.Winget.Domain;
using Ribanense.Solucoes.App.Winget.Services.Diagnostics;

namespace Ribanense.Solucoes.App.Winget.Services.Sources;

public sealed class WingetSourceService : IWingetSourceService
{
    private readonly IWingetExecutor _executor;
    private readonly IElevatedCommandRunner _elevated;

    public WingetSourceService(IWingetExecutor executor, IElevatedCommandRunner elevated)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _elevated = elevated ?? throw new ArgumentNullException(nameof(elevated));
    }

    public async Task<IReadOnlyList<WingetSource>> ListAsync(CancellationToken ct)
    {
        var args = new[]
        {
            "source", "list",
            "--disable-interactivity",
            "--accept-source-agreements"
        };
        var result = await _executor.RunAsync(args, ct: ct).ConfigureAwait(false);
        return ParseListOutput(result.Stdout);
    }

    public Task<WingetRunResult> UpdateAsync(string? name, Action<string>? onLine, CancellationToken ct)
    {
        var args = new List<string> { "source", "update", "--disable-interactivity", "--accept-source-agreements" };
        if (!string.IsNullOrWhiteSpace(name))
        {
            args.Add("--name");
            args.Add(name);
        }
        return _executor.RunAsync(args, onStdout: onLine, onStderr: onLine, ct: ct);
    }

    public Task<WingetRunResult> AddAsync(string name, string argument, string type, Action<string>? onLine, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name obrigatorio.", nameof(name));
        if (string.IsNullOrWhiteSpace(argument)) throw new ArgumentException("argument obrigatorio.", nameof(argument));
        if (string.IsNullOrWhiteSpace(type)) throw new ArgumentException("type obrigatorio.", nameof(type));

        var args = new List<string>
        {
            "source", "add",
            "--name", name,
            "--arg", argument,
            "--type", type,
            "--disable-interactivity",
            "--accept-source-agreements"
        };
        return _executor.RunAsync(args, onStdout: onLine, onStderr: onLine, ct: ct);
    }

    public Task<WingetRunResult> RemoveAsync(string name, Action<string>? onLine, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name obrigatorio.", nameof(name));

        var args = new[]
        {
            "source", "remove",
            "--name", name,
            "--disable-interactivity"
        };
        return _executor.RunAsync(args, onStdout: onLine, onStderr: onLine, ct: ct);
    }

    public async Task<WingetRunResult> ResetAsync(IProgress<string>? onLine, CancellationToken ct)
    {
        // winget source reset --force precisa de admin.
        string script = "winget source reset --force --disable-interactivity --accept-source-agreements";
        var elevated = await _elevated.RunScriptAsync(script, onLine, ct).ConfigureAwait(false);
        return new WingetRunResult(
            ExitCode: elevated.ExitCode,
            Stdout: elevated.Output,
            Stderr: elevated.Cancelled ? "Operacao cancelada pelo usuario." : string.Empty);
    }

    public async Task<string> ExportAsync(CancellationToken ct)
    {
        var args = new[]
        {
            "source", "export",
            "--disable-interactivity",
            "--accept-source-agreements"
        };
        var result = await _executor.RunAsync(args, ct: ct).ConfigureAwait(false);
        return result.Stdout;
    }

    internal static IReadOnlyList<WingetSource> ParseListOutput(string stdout)
    {
        var table = WingetTableParser.Parse(stdout);
        if (table is null) return Array.Empty<WingetSource>();

        int nameIdx = ColumnIndex(table.Headers, ["Name", "Nome"]);
        int argIdx = ColumnIndex(table.Headers, ["Argument", "Argumento", "URL"]);
        int typeIdx = ColumnIndex(table.Headers, ["Type", "Tipo"]);
        int trustIdx = ColumnIndex(table.Headers, ["Trust Level", "Explicit", "Confianca", "Confiança"]);

        if (nameIdx < 0 || argIdx < 0) return Array.Empty<WingetSource>();

        var list = new List<WingetSource>(table.Rows.Count);
        foreach (var row in table.Rows)
        {
            if (row.Values.Count <= Math.Max(nameIdx, argIdx)) continue;
            if (string.IsNullOrWhiteSpace(row.Values[nameIdx])) continue;

            string type = typeIdx >= 0 && typeIdx < row.Values.Count ? row.Values[typeIdx] : string.Empty;
            string? trust = trustIdx >= 0 && trustIdx < row.Values.Count
                ? (string.IsNullOrWhiteSpace(row.Values[trustIdx]) ? null : row.Values[trustIdx])
                : null;

            list.Add(new WingetSource(
                Name: row.Values[nameIdx],
                Argument: row.Values[argIdx],
                Type: type,
                TrustLevel: trust));
        }
        return list;
    }

    private static int ColumnIndex(IReadOnlyList<string> headers, string[] candidates)
    {
        for (int i = 0; i < headers.Count; i++)
        {
            foreach (var c in candidates)
            {
                if (headers[i].Equals(c, StringComparison.OrdinalIgnoreCase)) return i;
            }
        }
        return -1;
    }
}
