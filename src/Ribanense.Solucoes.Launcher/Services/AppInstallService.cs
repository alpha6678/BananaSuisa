using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Ribanense.Solucoes.Launcher.Domain;
using Ribanense.Solucoes.PluginSDK.Logging;

namespace Ribanense.Solucoes.Launcher.Services;

/// <summary>
/// Download + validacao SHA256 + extracao atomica com swap de pasta.
/// Se o app estiver em execucao, tenta encerra-lo antes do swap de pasta.
/// </summary>
public sealed class AppInstallService : IAppInstallService
{
    private readonly IGitHubClient _github;
    private readonly IInstalledAppsRegistry _registry;
    private readonly IAppJsonLog _log;

    public AppInstallService(IGitHubClient github, IInstalledAppsRegistry registry, IAppJsonLog log)
    {
        _github = github ?? throw new ArgumentNullException(nameof(github));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<AppInstallResult> InstallAsync(AppInstallRequest request, CancellationToken ct)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        if (AppProcessDetector.IsRunning(request.AppId))
        {
            var installed = _registry.Find(request.AplicativosRoot, request.AppId);
            _log.Write(AppLogLevel.Information, "install.close",
                $"Encerrando {request.AppId} em execução para permitir instalação/atualização.");

            if (!AppProcessDetector.TryCloseRunning(
                    request.AppId,
                    installed?.ExecutablePath,
                    TimeSpan.FromSeconds(8)))
            {
                return new AppInstallResult(false,
                    "Não foi possível encerrar o app em execução. Feche-o e tente novamente.", null);
            }
        }

        var zipAsset = request.Release.ZipAsset;
        if (zipAsset is null)
        {
            return new AppInstallResult(false,
                $"Release {request.Release.Tag} não tem asset .zip.", null);
        }

        _log.Write(AppLogLevel.Information, "install.start",
            $"Instalando {request.AppId} {request.Release.Version}.");

        byte[] zipBytes;
        try
        {
            zipBytes = await _github.GetBytesAsync(zipAsset.DownloadUrl, request.Progress, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Write(AppLogLevel.Error, "install.download", "Falha no download do zip.", ex);
            return new AppInstallResult(false, $"Falha no download: {ex.Message}", null);
        }

        var shaAsset = request.Release.Sha256Asset;
        if (shaAsset is not null)
        {
            try
            {
                byte[] shaBytes = await _github.GetBytesAsync(shaAsset.DownloadUrl, null, ct).ConfigureAwait(false);
                string shaText = Encoding.UTF8.GetString(shaBytes).Trim();
                string expected = ExtractHash(shaText);

                string actual = ComputeSha256(zipBytes);
                if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                {
                    _log.Write(AppLogLevel.Error, "install.hash",
                        $"SHA256 divergente. esperado={expected} obtido={actual}");
                    return new AppInstallResult(false,
                        $"SHA256 do zip não confere. Esperado {expected}, obtido {actual}.", null);
                }
            }
            catch (Exception ex)
            {
                _log.Write(AppLogLevel.Warning, "install.hash",
                    "Falha ao validar SHA256 (prosseguindo sem validação).", ex);
            }
        }

        Directory.CreateDirectory(request.AplicativosRoot);
        string slug = DeriveSlug(request.AppId);
        string finalPath = Path.Combine(request.AplicativosRoot, slug);
        string tmpPath = finalPath + ".tmp-" + Guid.NewGuid().ToString("N").Substring(0, 8);

        try
        {
            Directory.CreateDirectory(tmpPath);
            using (var ms = new MemoryStream(zipBytes))
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Read))
            {
                archive.ExtractToDirectory(tmpPath, overwriteFiles: true);
            }
        }
        catch (Exception ex)
        {
            TryDelete(tmpPath);
            _log.Write(AppLogLevel.Error, "install.extract", "Falha ao extrair zip.", ex);
            return new AppInstallResult(false, $"Falha ao extrair: {ex.Message}", null);
        }

        string? backupPath = null;
        try
        {
            if (Directory.Exists(finalPath))
            {
                backupPath = finalPath + ".bak-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                Directory.Move(finalPath, backupPath);
            }

            Directory.Move(tmpPath, finalPath);
        }
        catch (Exception ex)
        {
            // rollback
            try
            {
                if (Directory.Exists(finalPath)) Directory.Delete(finalPath, recursive: true);
                if (backupPath is not null && Directory.Exists(backupPath))
                {
                    Directory.Move(backupPath, finalPath);
                }
            }
            catch
            {
                // best effort
            }
            TryDelete(tmpPath);
            _log.Write(AppLogLevel.Error, "install.swap", "Falha no swap atomico de pasta.", ex);
            return new AppInstallResult(false, $"Falha no swap: {ex.Message}", null);
        }

        if (backupPath is not null)
        {
            TryDelete(backupPath);
        }

        _log.Write(AppLogLevel.Information, "install.done",
            $"{request.AppId} {request.Release.Version} instalado em {finalPath}.");

        return new AppInstallResult(true, null, finalPath);
    }

    public AppUninstallResult Uninstall(string aplicativosRoot, string appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return new AppUninstallResult(false, "appId obrigatório.");
        }

        var installed = _registry.Find(aplicativosRoot, appId);
        if (installed is null)
        {
            return new AppUninstallResult(false, "App não está instalado.");
        }

        if (AppProcessDetector.IsRunning(appId))
        {
            _log.Write(AppLogLevel.Information, "uninstall.close",
                $"Encerrando {appId} em execução para permitir desinstalação.");

            if (!AppProcessDetector.TryCloseRunning(appId, installed.ExecutablePath, TimeSpan.FromSeconds(8)))
            {
                return new AppUninstallResult(false,
                    "Não foi possível encerrar o app em execução. Feche-o e tente novamente.");
            }
        }

        try
        {
            Directory.Delete(installed.InstallPath, recursive: true);
            _log.Write(AppLogLevel.Information, "uninstall.done", $"{appId} removido.");
            return new AppUninstallResult(true, null);
        }
        catch (Exception ex)
        {
            _log.Write(AppLogLevel.Error, "uninstall.fail", "Falha ao remover pasta.", ex);
            return new AppUninstallResult(false, ex.Message);
        }
    }

    private static string ComputeSha256(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
    }

    private static string ExtractHash(string shaText)
    {
        // formato comum: "<hash>  <filename>" ou somente "<hash>".
        string line = shaText.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            ?? string.Empty;
        string head = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty;
        return head.Trim().ToLowerInvariant();
    }

    private static string DeriveSlug(string appId)
    {
        int lastDot = appId.LastIndexOf('.');
        string slug = lastDot >= 0 ? appId[(lastDot + 1)..] : appId;
        if (string.IsNullOrWhiteSpace(slug)) slug = "app";

        // Primeira letra maiuscula para visual consistente ("winget" -> "Winget").
        return char.ToUpperInvariant(slug[0]) + slug[1..];
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // best effort
        }
    }
}
