using System.IO;
using Ribanense.Solucoes.App.Farol.Domain;
using Ribanense.Solucoes.Infrastructure.Vault;
using Ribanense.Solucoes.PluginSDK;
using Ribanense.Solucoes.PluginSDK.Logging;

namespace Ribanense.Solucoes.App.Farol.Collectors;

/// <summary>
/// Varre <c>%LOCALAPPDATA%\Ribanense Soluções\apps\</c> e resume o estado de cada
/// app irmão a partir do próprio vault que eles já escrevem via <see cref="IAppJsonLog"/>.
/// </summary>
public sealed class RibanenseLogsCollector : ICollector
{
    private readonly string _appsRoot;
    private readonly string _selfAppId;

    public RibanenseLogsCollector(string? appsRoot = null, string? selfAppId = null)
    {
        _appsRoot = appsRoot ?? DefaultAppsRoot();
        _selfAppId = selfAppId ?? Configuration.FarolAppConfig.AppId;
    }

    public string Id => "ribanense-apps";
    public string DisplayName => "Apps Ribanense";

    public static string DefaultAppsRoot() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppPaths.ProductFolderName,
        "apps");

    public Task CollectAsync(EvidenceBundleBuilder builder, CancellationToken ct)
    {
        if (!Directory.Exists(_appsRoot)) return Task.CompletedTask;

        foreach (string appDir in Directory.EnumerateDirectories(_appsRoot))
        {
            ct.ThrowIfCancellationRequested();

            string appId = Path.GetFileName(appDir);
            if (string.Equals(appId, _selfAppId, StringComparison.OrdinalIgnoreCase)) continue;

            string? vaultPath = Directory.EnumerateFiles(appDir, "*.dat").FirstOrDefault();
            if (vaultPath is null)
            {
                builder.RibanenseApps.Add(new RibanenseAppInfo(appId, null, false, 0, null, null));
                continue;
            }

            builder.RibanenseApps.Add(Summarize(appId, vaultPath));
        }

        return Task.CompletedTask;
    }

    private static RibanenseAppInfo Summarize(string appId, string vaultPath)
    {
        try
        {
            using var vault = new LiteDbVault(vaultPath);
            IReadOnlyList<JsonLogEntry> logs = vault.GetRecentLogs(200);

            var errors = logs
                .Where(l => IsFailure(l.Level))
                .OrderByDescending(l => l.TimestampUtc)
                .ToArray();

            JsonLogEntry? last = errors.FirstOrDefault();

            return new RibanenseAppInfo(
                AppId: appId,
                Version: logs.OrderByDescending(l => l.TimestampUtc).FirstOrDefault()?.AppVersion,
                VaultPresent: true,
                RecentErrorCount: errors.Length,
                LastErrorMessage: last is null ? null : EventLogCollector.Truncate(last.Message, 300),
                LastErrorAt: last is null ? null : new DateTimeOffset(last.TimestampUtc, TimeSpan.Zero));
        }
        catch (IOException)
        {
            // Vault aberto pelo próprio app: leitura concorrente bloqueada é esperada.
            return new RibanenseAppInfo(appId, null, true, 0, "Vault em uso pelo app.", null);
        }
        catch (Exception ex)
        {
            return new RibanenseAppInfo(appId, null, true, 0, EventLogCollector.Truncate(ex.Message, 200), null);
        }
    }

    internal static bool IsFailure(string? level) =>
        string.Equals(level, nameof(AppLogLevel.Error), StringComparison.OrdinalIgnoreCase)
        || string.Equals(level, nameof(AppLogLevel.Critical), StringComparison.OrdinalIgnoreCase);
}
