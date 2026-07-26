using System.Diagnostics;
using System.IO;
using System.Text;
using Ribanense.Solucoes.Launcher.Configuration;
using Ribanense.Solucoes.Launcher.Domain;
using Ribanense.Solucoes.PluginSDK;
using Ribanense.Solucoes.PluginSDK.Logging;

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

    public LauncherUpdateService(IReleaseCheckService releases, IGitHubClient github, IAppJsonLog log)
    {
        _releases = releases ?? throw new ArgumentNullException(nameof(releases));
        _github = github ?? throw new ArgumentNullException(nameof(github));
        _log = log ?? throw new ArgumentNullException(nameof(log));
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
                UseShellExecute = false,
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
