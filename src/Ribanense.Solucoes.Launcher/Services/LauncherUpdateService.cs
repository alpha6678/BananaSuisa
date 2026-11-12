using System.Diagnostics;
using System.IO;
using System.Text;
using Ribanense.Solucoes.Launcher.Configuration;
using Ribanense.Solucoes.Launcher.Domain;
using Ribanense.Solucoes.PluginSDK;
using Ribanense.Solucoes.PluginSDK.Logging;
using Ribanense.Solucoes.PluginSDK.Payload;

namespace Ribanense.Solucoes.Launcher.Services;

/// <summary>
/// Auto-atualizacao do launcher: verifica release <c>launcher-v*</c>, baixa o .exe single-file,
/// valida SHA256 e instala o binario com o nome do asset via rename-and-swap coordenado por PID.
/// </summary>
public sealed class LauncherUpdateService : ILauncherUpdateService
{
    /// <summary>Argumento que sinaliza um processo recem-iniciado apos uma atualizacao.</summary>
    public const string PostUpdateArg = "--post-update";

    private readonly IReleaseCheckService _releases;
    private readonly IGitHubClient _github;
    private readonly IAppJsonLog _log;
    private readonly PayloadUpdateService _payload;

    public LauncherUpdateService(IReleaseCheckService releases, IGitHubClient github, IAppJsonLog log)
    {
        _releases = releases ?? throw new ArgumentNullException(nameof(releases));
        _github = github ?? throw new ArgumentNullException(nameof(github));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _payload = new PayloadUpdateService(github, log);
    }

    public async Task<ReleaseInfo?> CheckForUpdateAsync(CancellationToken ct)
    {
        try
        {
            var latest = await _releases.GetLatestReleaseAsync(
                LauncherConfig.LauncherGithubOwner,
                LauncherConfig.LauncherGithubRepo,
                LauncherConfig.LauncherTagPrefix,
                includePrerelease: false,
                ct).ConfigureAwait(false);

            if (latest is null) return null;

            string current = AppVersion.ForEntry();
            var status = _releases.CompareVersions(current, latest.Version);
            return status == UpdateStatus.UpdateAvailable ? latest : null;
        }
        catch (Exception ex)
        {
            _log.Write(AppLogLevel.Warning, "launcher.update.check",
                "Falha ao verificar atualizacao do launcher.", ex);
            return null;
        }
    }

    public async Task<LauncherUpdateResult> DownloadAndApplyAsync(
        ReleaseInfo release, IProgress<double>? progress, CancellationToken ct)
    {
        if (release is null) throw new ArgumentNullException(nameof(release));

        string? exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            return new LauncherUpdateResult(false,
                "Nao foi possivel localizar o executavel atual do launcher.");
        }

        string? dir = Path.GetDirectoryName(exePath);
        if (string.IsNullOrWhiteSpace(dir))
        {
            return new LauncherUpdateResult(false,
                "Nao foi possivel determinar a pasta do launcher.");
        }

        if (!IsDirectoryWritable(dir))
        {
            return new LauncherUpdateResult(false,
                $"A pasta do launcher nao permite gravacao:\n{dir}\n\n" +
                "Mova o launcher para uma pasta gravavel (ex.: fora de Program Files) " +
                "ou execute como administrador e tente novamente.");
        }

        if (ShouldUseDelta(release))
            return await ApplyFolderPayloadAsync(release, progress, ct).ConfigureAwait(false);

        var exeAsset = release.ExeAsset;
        if (exeAsset is null)
        {
            return new LauncherUpdateResult(false,
                $"A release {release.Tag} nao possui asset .exe.");
        }

        string targetPath;
        try
        {
            targetPath = GetTargetExecutablePath(exePath, exeAsset.Name);
        }
        catch (ArgumentException ex)
        {
            _log.Write(AppLogLevel.Error, "launcher.update.asset-name",
                $"Nome de asset invalido: {exeAsset.Name}", ex);
            return new LauncherUpdateResult(false,
                $"O nome do executavel da release e invalido: {exeAsset.Name}");
        }

        _log.Write(AppLogLevel.Information, "launcher.update.start",
            $"Baixando launcher {release.Version}.");

        byte[] exeBytes;
        try
        {
            exeBytes = await _github.GetBytesAsync(exeAsset.DownloadUrl, progress, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Write(AppLogLevel.Error, "launcher.update.download", "Falha no download do .exe.", ex);
            return new LauncherUpdateResult(false, $"Falha no download: {ex.Message}");
        }

        var shaAsset = release.ExeSha256Asset;
        if (shaAsset is not null)
        {
            try
            {
                byte[] shaBytes = await _github.GetBytesAsync(shaAsset.DownloadUrl, null, ct).ConfigureAwait(false);
                string expected = Sha256Util.ExtractHash(Encoding.UTF8.GetString(shaBytes).Trim());
                string actual = Sha256Util.Compute(exeBytes);
                if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                {
                    _log.Write(AppLogLevel.Error, "launcher.update.hash",
                        $"SHA256 divergente. esperado={expected} obtido={actual}");
                    return new LauncherUpdateResult(false,
                        $"SHA256 do executavel nao confere. Atualizacao cancelada por seguranca.");
                }
            }
            catch (Exception ex)
            {
                _log.Write(AppLogLevel.Warning, "launcher.update.hash",
                    "Falha ao validar SHA256 (prosseguindo sem validacao).", ex);
            }
        }

        string suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
        string newPath = targetPath + $".new-{suffix}.exe";
        string oldPath = exePath + $".old-{suffix}.exe";
        string? displacedTargetPath = PathsEqual(exePath, targetPath)
            ? null
            : targetPath + $".old-{suffix}.exe";

        try
        {
            await File.WriteAllBytesAsync(newPath, exeBytes, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            TryDelete(newPath);
            _log.Write(AppLogLevel.Error, "launcher.update.write", "Falha ao gravar novo executavel.", ex);
            return new LauncherUpdateResult(false, $"Falha ao gravar o novo executavel: {ex.Message}");
        }

        // Renomeia o exe em execucao (permitido no Windows) e instala o novo usando
        // o nome versionado do asset da release.
        bool targetDisplaced = false;
        bool currentRenamed = false;
        bool newInstalled = false;
        try
        {
            if (displacedTargetPath is not null && File.Exists(targetPath))
            {
                File.Move(targetPath, displacedTargetPath);
                targetDisplaced = true;
            }

            File.Move(exePath, oldPath);
            currentRenamed = true;

            File.Move(newPath, targetPath);
            newInstalled = true;
        }
        catch (Exception ex)
        {
            RollbackSwap(
                exePath,
                targetPath,
                oldPath,
                displacedTargetPath,
                targetDisplaced,
                currentRenamed,
                newInstalled);
            TryDelete(newPath);
            _log.Write(AppLogLevel.Error, "launcher.update.swap",
                "Falha ao instalar o novo executavel com o nome da release.", ex);
            return new LauncherUpdateResult(false, $"Falha ao aplicar a atualizacao: {ex.Message}");
        }

        // Inicia o novo processo, que aguarda este encerrar e limpa o binario antigo.
        try
        {
            var psi = new ProcessStartInfo(targetPath)
            {
                UseShellExecute = true,
                WorkingDirectory = dir
            };
            psi.ArgumentList.Add(PostUpdateArg);
            psi.ArgumentList.Add(Environment.ProcessId.ToString());
            psi.ArgumentList.Add(oldPath);
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            RollbackSwap(
                exePath,
                targetPath,
                oldPath,
                displacedTargetPath,
                targetDisplaced,
                currentRenamed,
                newInstalled);
            _log.Write(AppLogLevel.Error, "launcher.update.relaunch", "Falha ao iniciar o novo launcher.", ex);
            return new LauncherUpdateResult(false, $"Falha ao iniciar a nova versao: {ex.Message}");
        }

        _log.Write(AppLogLevel.Information, "launcher.update.done",
            $"Launcher {release.Version} aplicado como {Path.GetFileName(targetPath)}; reiniciando.");
        return new LauncherUpdateResult(true, null);
    }

    /// <summary>
    /// Cópia instalada/pasta (com <c>payload-manifest.json</c>) atualiza por delta/zip.
    /// Cópia avulsa — o <c>.exe</c> único — continua trocando o arquivo inteiro, como antes.
    /// </summary>
    internal static bool ShouldUseDelta(ReleaseInfo release, bool? installedCopy = null)
    {
        if (release is null) throw new ArgumentNullException(nameof(release));
        if (release.ZipAsset is null) return false;

        bool managed = installedCopy ?? HasPayloadManifest(CurrentDirectory());
        return managed;
    }

    internal static string CurrentDirectory()
    {
        string? exe = Environment.ProcessPath;
        return string.IsNullOrWhiteSpace(exe)
            ? Path.GetFullPath(AppContext.BaseDirectory)
            : Path.GetDirectoryName(exe) ?? Path.GetFullPath(AppContext.BaseDirectory);
    }

    internal static bool HasPayloadManifest(string? directory) =>
        !string.IsNullOrWhiteSpace(directory) &&
        File.Exists(Path.Combine(directory, PayloadManifest.FileName));

    private async Task<LauncherUpdateResult> ApplyFolderPayloadAsync(
        ReleaseInfo release, IProgress<double>? progress, CancellationToken ct)
    {
        string? exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            return new LauncherUpdateResult(false,
                "Nao foi possivel localizar o executavel atual do launcher.");
        }

        string? currentDir = Path.GetDirectoryName(exePath);
        if (string.IsNullOrWhiteSpace(currentDir))
        {
            return new LauncherUpdateResult(false,
                "Nao foi possivel determinar a pasta do launcher.");
        }

        bool managed = HasPayloadManifest(currentDir);
        string finalDir = managed ? currentDir : DeploymentLayout.DefaultInstallDirectory;
        string? parent = Path.GetDirectoryName(finalDir);
        if (string.IsNullOrWhiteSpace(parent))
        {
            return new LauncherUpdateResult(false,
                "Nao foi possivel determinar a pasta de instalacao do launcher.");
        }

        try
        {
            Directory.CreateDirectory(parent);
        }
        catch (Exception ex)
        {
            return new LauncherUpdateResult(false, $"Nao foi possivel criar a pasta de instalacao: {ex.Message}");
        }

        if (!IsDirectoryWritable(parent))
        {
            return new LauncherUpdateResult(false,
                $"A pasta do launcher nao permite gravacao:\n{parent}");
        }

        IReadOnlyList<ReleaseInfo> history;
        try
        {
            history = await _releases.GetAllReleasesAsync(
                LauncherConfig.LauncherGithubOwner,
                LauncherConfig.LauncherGithubRepo,
                LauncherConfig.LauncherTagPrefix,
                ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Write(AppLogLevel.Warning, "launcher.update.history",
                "Falha ao listar releases; o pacote completo sera usado.", ex);
            history = Array.Empty<ReleaseInfo>();
        }

        string suffix = Guid.NewGuid().ToString("N")[..8];
        string staging = Path.Combine(parent, Path.GetFileName(finalDir) + ".new-" + suffix);
        string oldDir = Path.Combine(parent, Path.GetFileName(finalDir) + ".old-" + suffix);
        string? installedVersion = managed ? ReadPayloadVersion(currentDir) : null;

        PayloadStageResult staged = await _payload.StageAsync(
            managed ? currentDir : null,
            installedVersion,
            release,
            history,
            staging,
            progress,
            ct).ConfigureAwait(false);

        if (!staged.Success)
        {
            TryDeleteDirectory(staging);
            return new LauncherUpdateResult(false, staged.Error ?? "Falha ao preparar a atualizacao.");
        }

        string entryName = ReadEntryExecutable(staging);
        string stagedExe = Path.Combine(staging, entryName);
        if (!File.Exists(stagedExe))
        {
            TryDeleteDirectory(staging);
            return new LauncherUpdateResult(false,
                $"O pacote nao contem {entryName}.");
        }

        bool hadFinal = Directory.Exists(finalDir);
        bool swapped = false;
        try
        {
            if (hadFinal)
                Directory.Move(finalDir, oldDir);
            Directory.Move(staging, finalDir);
            swapped = true;

            string launched = Path.Combine(finalDir, entryName);
            var psi = new ProcessStartInfo(launched)
            {
                UseShellExecute = true,
                WorkingDirectory = finalDir
            };
            psi.ArgumentList.Add(PostUpdateArg);
            psi.ArgumentList.Add(Environment.ProcessId.ToString());
            string? cleanup = null;
            if (hadFinal && Directory.Exists(oldDir))
                cleanup = oldDir;
            else if (!managed && IsLegacySingleFile(exePath))
                cleanup = exePath;
            if (cleanup is not null)
                psi.ArgumentList.Add(cleanup);

            if (Process.Start(psi) is null)
                throw new InvalidOperationException("Processo novo nao iniciou.");
        }
        catch (Exception ex)
        {
            RollbackFolderSwap(finalDir, staging, oldDir, hadFinal, swapped);
            _log.Write(AppLogLevel.Error, "launcher.update.swap",
                "Falha ao trocar a pasta do launcher.", ex);
            return new LauncherUpdateResult(false, $"Falha ao aplicar a atualizacao: {ex.Message}");
        }

        _log.Write(AppLogLevel.Information, "launcher.update.done",
            $"Launcher {release.Version} aplicado em {finalDir}; reiniciando.");
        return new LauncherUpdateResult(true, null);
    }

    internal static string ResolveFolderTarget(string currentExecutablePath, bool payloadManifestPresent) =>
        payloadManifestPresent
            ? Path.GetDirectoryName(currentExecutablePath) ?? currentExecutablePath
            : DeploymentLayout.DefaultInstallDirectory;

    private static string? ReadPayloadVersion(string directory)
    {
        try
        {
            string path = Path.Combine(directory, PayloadManifest.FileName);
            if (!File.Exists(path)) return null;
            return PayloadManifest.Load(path).Version;
        }
        catch
        {
            return null;
        }
    }

    private static string ReadEntryExecutable(string directory)
    {
        try
        {
            string path = Path.Combine(directory, PayloadManifest.FileName);
            if (File.Exists(path))
            {
                string entry = PayloadManifest.Load(path).EntryExecutable;
                if (!string.IsNullOrWhiteSpace(entry))
                    return entry.Replace('/', Path.DirectorySeparatorChar);
            }
        }
        catch
        {
            // cai no nome estavel
        }

        return DeploymentLayout.LauncherExecutableName;
    }

    private static bool IsLegacySingleFile(string exePath)
    {
        string name = Path.GetFileName(exePath);
        return name.StartsWith("launcher-", StringComparison.OrdinalIgnoreCase) &&
            name.EndsWith("-win-x64.exe", StringComparison.OrdinalIgnoreCase);
    }

    private static void RollbackFolderSwap(
        string finalDir, string staging, string oldDir, bool hadFinal, bool swapped)
    {
        if (!swapped)
        {
            TryDeleteDirectory(staging);
            return;
        }

        try
        {
            if (Directory.Exists(finalDir))
                Directory.Move(finalDir, staging);
        }
        catch
        {
            // a pasta nova ficou no lugar; nao apaga
        }

        try
        {
            if (hadFinal && Directory.Exists(oldDir) && !Directory.Exists(finalDir))
                Directory.Move(oldDir, finalDir);
        }
        catch
        {
            // best effort
        }
    }

    private static void TryDeleteDirectory(string path)
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

    internal static string GetTargetExecutablePath(string currentExecutablePath, string assetName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentExecutablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetName);

        string? dir = Path.GetDirectoryName(currentExecutablePath);
        string fileName = Path.GetFileName(assetName.Trim());

        if (string.IsNullOrWhiteSpace(dir) ||
            string.IsNullOrWhiteSpace(fileName) ||
            fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            !fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("O asset nao possui um nome de executavel valido.", nameof(assetName));
        }

        return Path.Combine(dir, fileName);
    }

    internal static string? GetLegacyFileNameMigrationTarget(
        string currentExecutablePath,
        string currentVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentExecutablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentVersion);

        const string prefix = "launcher-";
        const string suffix = "-win-x64.exe";
        string fileName = Path.GetFileName(currentExecutablePath);

        if (!fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string legacyVersion = fileName[prefix.Length..^suffix.Length];
        string numericVersion = legacyVersion.Split('-', 2)[0];
        if (numericVersion.Count(c => c == '.') < 2 ||
            !Version.TryParse(numericVersion, out _))
        {
            return null;
        }

        string expectedName = $"launcher-{currentVersion}-win-x64.exe";
        string targetPath = GetTargetExecutablePath(currentExecutablePath, expectedName);
        return PathsEqual(currentExecutablePath, targetPath) ? null : targetPath;
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);

    private static void RollbackSwap(
        string currentPath,
        string targetPath,
        string oldPath,
        string? displacedTargetPath,
        bool targetDisplaced,
        bool currentRenamed,
        bool newInstalled)
    {
        if (newInstalled)
        {
            TryDelete(targetPath);
        }

        if (currentRenamed)
        {
            TryMove(oldPath, currentPath);
        }

        if (targetDisplaced && displacedTargetPath is not null)
        {
            TryMove(displacedTargetPath, targetPath);
        }
    }

    private static bool IsDirectoryWritable(string dir)
    {
        try
        {
            string probe = Path.Combine(dir, $".rb-write-{Guid.NewGuid():N}.tmp");
            using (File.Create(probe)) { }
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void TryMove(string source, string destination)
    {
        try
        {
            if (File.Exists(source) && !File.Exists(destination))
            {
                File.Move(source, destination);
            }
        }
        catch
        {
            // best effort
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // best effort
        }
    }
}
