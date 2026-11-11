using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Ribanense.Solucoes.Launcher.Domain;
using Ribanense.Solucoes.PluginSDK.Logging;
using Ribanense.Solucoes.PluginSDK.Manifest;
using Ribanense.Solucoes.PluginSDK.Payload;

namespace Ribanense.Solucoes.Launcher.Services;

/// <summary>
/// Instala um app por delta (quando a corrente existe) ou pelo zip completo,
/// com swap de pasta. Projetos parceiros usam o asset portátil ou o instalador deles.
/// </summary>
public sealed class AppInstallService : IAppInstallService
{
    private readonly IGitHubClient _github;
    private readonly IInstalledAppsRegistry _registry;
    private readonly IAppJsonLog _log;
    private readonly PayloadUpdateService _payload;
    private readonly InstallRegistrationService? _registration;
    private readonly string _launcherExe;

    public AppInstallService(
        IGitHubClient github,
        IInstalledAppsRegistry registry,
        IAppJsonLog log,
        IInstallRegistration? registration = null,
        string? launcherExe = null)
    {
        _github = github ?? throw new ArgumentNullException(nameof(github));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _payload = new PayloadUpdateService(github, log);
        _registration = registration is null ? null : new InstallRegistrationService(registration);
        _launcherExe = string.IsNullOrWhiteSpace(launcherExe)
            ? Environment.ProcessPath ?? string.Empty
            : launcherExe;
    }

    public async Task<AppInstallResult> InstallAsync(AppInstallRequest request, CancellationToken ct)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        if (request.Entry?.IsPartner == true)
            return await InstallPartnerAsync(request, ct).ConfigureAwait(false);

        if (AppProcessDetector.IsRunning(request.AppId))
        {
            var running = _registry.Find(request.AplicativosRoot, request.AppId);
            _log.Write(AppLogLevel.Information, "install.close",
                $"Encerrando {request.AppId} em execução para permitir instalação/atualização.");

            if (!AppProcessDetector.TryCloseRunning(
                    request.AppId,
                    running?.ExecutablePath,
                    TimeSpan.FromSeconds(8)))
            {
                return new AppInstallResult(false,
                    "Não foi possível encerrar o app em execução. Feche-o e tente novamente.", null);
            }
        }

        Directory.CreateDirectory(request.AplicativosRoot);
        string slug = AppSlug.FromId(request.AppId);
        string finalPath = Path.Combine(request.AplicativosRoot, slug);
        InstalledApp? installed = _registry.Find(request.AplicativosRoot, request.AppId);
        AppInstallMode effective = request.Mode
            ?? (installed is null ? AppInstallMode.Portable : installed.Mode);

        string tmpPath = finalPath + ".tmp-" + Guid.NewGuid().ToString("N")[..8];
        IReadOnlyList<ReleaseInfo> history = request.History ?? Array.Empty<ReleaseInfo>();

        PayloadStageResult staged = await _payload.StageAsync(
            Directory.Exists(finalPath) ? finalPath : null,
            installed?.Version,
            request.Release,
            history,
            tmpPath,
            request.Progress,
            ct).ConfigureAwait(false);

        if (!staged.Success)
        {
            TryDelete(tmpPath);
            return new AppInstallResult(false, staged.Error ?? "Falha ao preparar o pacote.", null);
        }

        string? backupPath = null;
        try
        {
            if (Directory.Exists(finalPath))
            {
                backupPath = finalPath + ".bak-" + Guid.NewGuid().ToString("N")[..8];
                Directory.Move(finalPath, backupPath);
            }

            Directory.Move(tmpPath, finalPath);
        }
        catch (Exception ex)
        {
            try
            {
                if (Directory.Exists(finalPath)) Directory.Delete(finalPath, recursive: true);
                if (backupPath is not null && Directory.Exists(backupPath))
                    Directory.Move(backupPath, finalPath);
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
            TryDelete(backupPath);

        ApplyRegistration(effective, request.AppId, request.Entry, installed, finalPath);

        _log.Write(AppLogLevel.Information, "install.done",
            $"{request.AppId} {request.Release.Version} instalado em {finalPath} ({staged.Kind}).");

        return new AppInstallResult(true, null, finalPath);
    }

    public AppUninstallResult Uninstall(string aplicativosRoot, string appId, InstalledApp? known = null)
    {
        if (string.IsNullOrWhiteSpace(appId))
            return new AppUninstallResult(false, "appId obrigatório.");

        InstalledApp? installed = known ?? _registry.Find(aplicativosRoot, appId);
        if (installed is null)
            return new AppUninstallResult(false, "App não está instalado.");

        if (installed.ManagedByLauncher && AppProcessDetector.IsRunning(appId))
        {
            _log.Write(AppLogLevel.Information, "uninstall.close",
                $"Encerrando {appId} em execução para permitir desinstalação.");

            if (!AppProcessDetector.TryCloseRunning(appId, installed.ExecutablePath, TimeSpan.FromSeconds(8)))
            {
                return new AppUninstallResult(false,
                    "Não foi possível encerrar o app em execução. Feche-o e tente novamente.");
            }
        }

        _registration?.RemoveApp(appId, installed.DisplayName);

        bool insideLauncher = !string.IsNullOrWhiteSpace(aplicativosRoot) &&
            installed.InstallPath.StartsWith(
                Path.GetFullPath(aplicativosRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);

        if (!installed.ManagedByLauncher || !insideLauncher)
            return UninstallExternal(installed);

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

    private async Task<AppInstallResult> InstallPartnerAsync(AppInstallRequest request, CancellationToken ct)
    {
        CatalogEntry entry = request.Entry!;
        ReleaseInfo release = request.Release;
        ReleaseAsset? portable = release.FindByPattern(entry.PortableAsset);
        ReleaseAsset? installer = release.FindByPattern(entry.InstallerAsset);

        bool preferSetup = request.Mode == AppInstallMode.Registered && installer is not null;
        if (!preferSetup && portable is not null)
            return await InstallPartnerPortableAsync(request, portable, ct).ConfigureAwait(false);
        if (installer is not null)
            return await InstallPartnerSetupAsync(request, installer, ct).ConfigureAwait(false);

        return new AppInstallResult(false,
            $"A release {release.Tag} de {entry.DisplayName} não tem executável portátil nem instalador.",
            null);
    }

    private async Task<AppInstallResult> InstallPartnerPortableAsync(
        AppInstallRequest request, ReleaseAsset portable, CancellationToken ct)
    {
        CatalogEntry entry = request.Entry!;
        Directory.CreateDirectory(request.AplicativosRoot);
        string finalPath = Path.Combine(request.AplicativosRoot, AppSlug.FromId(request.AppId));
        string tmpPath = finalPath + ".tmp-" + Guid.NewGuid().ToString("N")[..8];
        Directory.CreateDirectory(tmpPath);

        try
        {
            byte[] bytes = await DownloadPartnerAssetAsync(
                portable, request.Release.FindByPattern(request.Entry!.PortableShaAsset), ct).ConfigureAwait(false);
            string exeName = string.IsNullOrWhiteSpace(entry.EntryExecutable)
                ? Path.GetFileName(new Uri(portable.DownloadUrl).LocalPath)
                : entry.EntryExecutable;
            if (string.IsNullOrWhiteSpace(exeName)) exeName = portable.Name;
            await File.WriteAllBytesAsync(Path.Combine(tmpPath, exeName), bytes, ct).ConfigureAwait(false);
            File.WriteAllText(Path.Combine(tmpPath, "app.json"), PartnerManifest(entry, request.Release.Version, exeName).Serialize());
            InstallRegistrationService.WriteMode(tmpPath, AppInstallMode.Portable);
        }
        catch (Exception ex)
        {
            TryDelete(tmpPath);
            _log.Write(AppLogLevel.Error, "partner.portable", "Falha ao baixar o portátil do parceiro.", ex);
            return new AppInstallResult(false, ex.Message, null);
        }

        return SwapFolder(request.AppId, finalPath, tmpPath, request.Release.Version);
    }

    private async Task<AppInstallResult> InstallPartnerSetupAsync(
        AppInstallRequest request, ReleaseAsset installer, CancellationToken ct)
    {
        ReleaseAsset? sha = request.Release.FindByPattern(request.Entry!.InstallerShaAsset);
        if (sha is null)
        {
            return new AppInstallResult(false,
                "O instalador do parceiro não tem arquivo .sha256 publicado. Atualização cancelada.",
                null);
        }

        string tempRoot = Path.Combine(Path.GetTempPath(), "Ribanense", "parceiros");
        Directory.CreateDirectory(tempRoot);
        string setupPath = Path.Combine(tempRoot, installer.Name);

        try
        {
            byte[] bytes = await DownloadPartnerAssetAsync(installer, sha, ct).ConfigureAwait(false);
            await File.WriteAllBytesAsync(setupPath, bytes, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Write(AppLogLevel.Error, "partner.setup", "Falha ao baixar o instalador do parceiro.", ex);
            return new AppInstallResult(false, ex.Message, null);
        }

        try
        {
            var psi = new ProcessStartInfo(setupPath, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS")
            {
                UseShellExecute = true
            };
            using Process? process = Process.Start(psi);
            if (process is null)
                return new AppInstallResult(false, "Não foi possível iniciar o instalador do parceiro.", null);

            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            if (process.ExitCode != 0 && process.ExitCode != 3010)
            {
                return new AppInstallResult(false,
                    $"O instalador de {request.Entry!.DisplayName} terminou com código {process.ExitCode}.",
                    null);
            }
        }
        catch (Exception ex)
        {
            _log.Write(AppLogLevel.Error, "partner.setup", "Falha ao executar o instalador do parceiro.", ex);
            return new AppInstallResult(false, ex.Message, null);
        }

        _log.Write(AppLogLevel.Information, "partner.setup",
            $"{request.AppId} {request.Release.Version} instalado pelo setup do parceiro.");
        return new AppInstallResult(true, null, null);
    }

    private async Task<byte[]> DownloadPartnerAssetAsync(ReleaseAsset asset, ReleaseAsset? shaAsset, CancellationToken ct)
    {
        byte[] bytes = await _github.GetBytesAsync(asset.DownloadUrl, null, ct).ConfigureAwait(false);
        if (shaAsset is null) return bytes;

        byte[] shaBytes = await _github.GetBytesAsync(shaAsset.DownloadUrl, null, ct).ConfigureAwait(false);
        string expected = Sha256Util.ExtractHash(System.Text.Encoding.UTF8.GetString(shaBytes).Trim());
        string actual = Sha256Util.Compute(bytes);
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"SHA256 de {asset.Name} não confere. Esperado {expected}, obtido {actual}.");
        }

        return bytes;
    }

    private AppInstallResult SwapFolder(string appId, string finalPath, string tmpPath, string version)
    {
        string? backupPath = null;
        try
        {
            if (Directory.Exists(finalPath))
            {
                backupPath = finalPath + ".bak-" + Guid.NewGuid().ToString("N")[..8];
                Directory.Move(finalPath, backupPath);
            }

            Directory.Move(tmpPath, finalPath);
        }
        catch (Exception ex)
        {
            TryDelete(tmpPath);
            _log.Write(AppLogLevel.Error, "install.swap", "Falha no swap do parceiro.", ex);
            return new AppInstallResult(false, $"Falha no swap: {ex.Message}", null);
        }

        if (backupPath is not null) TryDelete(backupPath);
        _log.Write(AppLogLevel.Information, "partner.portable", $"{appId} {version} em {finalPath}.");
        return new AppInstallResult(true, null, finalPath);
    }

    private void ApplyRegistration(
        AppInstallMode mode,
        string appId,
        CatalogEntry? entry,
        InstalledApp? installed,
        string finalPath)
    {
        string exeName = installed?.Manifest.EntryExecutable ?? entry?.EntryExecutable ?? string.Empty;
        string manifestPath = Path.Combine(finalPath, "app.json");
        if (File.Exists(manifestPath))
        {
            try
            {
                AppManifest manifest = AppManifest.Load(manifestPath);
                if (!string.IsNullOrWhiteSpace(manifest.EntryExecutable))
                    exeName = manifest.EntryExecutable;
            }
            catch
            {
                // o registro usa o nome que já tínhamos
            }
        }

        string exePath = string.IsNullOrWhiteSpace(exeName)
            ? finalPath
            : Path.Combine(finalPath, exeName);
        string displayName = entry?.DisplayName ?? installed?.DisplayName ?? appId;
        string version = entry is null ? installed?.Version ?? string.Empty : string.Empty;
        if (File.Exists(manifestPath))
        {
            try { version = AppManifest.Load(manifestPath).Version; } catch { }
        }

        if (_registration is null)
        {
            InstallRegistrationService.WriteMode(finalPath, mode);
            return;
        }

        _registration.ApplyApp(mode, appId, displayName, version, finalPath, exePath, _launcherExe);
    }

    private AppUninstallResult UninstallExternal(InstalledApp installed)
    {
        string uninstaller = Path.Combine(installed.InstallPath, "unins000.exe");
        if (!File.Exists(uninstaller))
        {
            return new AppUninstallResult(false,
                "Este app foi instalado fora do launcher e o desinstalador não foi encontrado. Remova em Aplicativos e recursos.");
        }

        try
        {
            Process.Start(new ProcessStartInfo(uninstaller, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART")
            {
                UseShellExecute = true
            });
            _log.Write(AppLogLevel.Information, "uninstall.external",
                $"Desinstalador de {installed.Id} iniciado.");
            return new AppUninstallResult(true, null);
        }
        catch (Exception ex)
        {
            _log.Write(AppLogLevel.Error, "uninstall.external", "Falha ao iniciar o desinstalador.", ex);
            return new AppUninstallResult(false, ex.Message);
        }
    }

    internal static AppManifest PartnerManifest(CatalogEntry entry, string version, string exeName) =>
        new()
        {
            Id = entry.Id,
            Name = string.IsNullOrWhiteSpace(entry.Name) ? entry.DisplayName : entry.Name,
            PublicName = entry.DisplayName,
            Version = version,
            MinimumLauncherVersion = string.IsNullOrWhiteSpace(entry.MinimumLauncherVersion)
                ? "0.1.0"
                : entry.MinimumLauncherVersion,
            EntryExecutable = exeName,
            Category = entry.Category,
            GithubTagPrefix = entry.GithubTagPrefix
        };

    internal static string? NormalizeVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        string trimmed = text.Trim();
        string first = trimmed.Split('\r', '\n')[0].Trim();
        if (SemVerLoose.IsValid(first)) return first;

        try
        {
            using JsonDocument doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.TryGetProperty("version", out JsonElement version))
            {
                string? parsed = NormalizeVersion(version.GetString());
                if (parsed is not null) return parsed;
            }
        }
        catch
        {
            // não é JSON
        }

        foreach (string token in trimmed.Split(new[] { ' ', '\t', '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (SemVerLoose.IsValid(token)) return token;
            string[] parts = token.Split('.');
            if (parts.Length >= 3 &&
                parts.Take(3).All(p => int.TryParse(p, out _)))
            {
                string core = string.Join('.', parts.Take(3));
                if (SemVerLoose.IsValid(core)) return core;
            }
        }

        return null;
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
