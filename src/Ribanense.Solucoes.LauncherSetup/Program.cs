using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using Ribanense.Solucoes.PluginSDK;

namespace Ribanense.Solucoes.LauncherSetup;

/// <summary>
/// Setup do launcher: consulta a release mais recente, baixa o exe único,
/// confere o SHA256 e chama a instalação dele. Não carrega o launcher dentro
/// de si, então nunca fica velho.
/// </summary>
internal static class Program
{
    internal const string Owner = "alpha6678";
    internal const string Repo = "BananaSuisa";
    internal const string TagPrefix = "launcher-v";

    [STAThread]
    private static int Main(string[] args)
    {
        bool silent = args.Any(a => a.Equals("/S", StringComparison.OrdinalIgnoreCase)
            || a.Equals("--silent", StringComparison.OrdinalIgnoreCase));

        try
        {
            string logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppPaths.ProductFolderName,
                "setup.log");
            Log.Init(logPath);
            Log.Write($"Setup {AppVersion.ForEntry()} iniciado.");

            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("RibanenseSetup/" + AppVersion.ForEntry());
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            Uri apiUrl = new($"https://api.github.com/repos/{Owner}/{Repo}/releases?per_page=100");
            string json = http.GetStringAsync(apiUrl).GetAwaiter().GetResult();
            LatestRelease? latest = ReleasePicker.PickLatest(json, TagPrefix)
                ?? throw new InvalidOperationException($"Nenhuma release {TagPrefix} publicada.");

            Log.Write($"Versao mais recente: {latest.Version}.");
            AssetPick assets = ReleasePicker.PickLauncherAssets(latest.Assets, latest.Version);

            string workDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppPaths.ProductFolderName,
                "setup");
            if (Directory.Exists(workDir)) Directory.Delete(workDir, true);
            Directory.CreateDirectory(workDir);

            if (assets.Executable is not null)
            {
                string exePath = Path.Combine(workDir, assets.Executable.Name);
                Download(http, assets.Executable.Url, exePath);
                Log.Write($"Baixado em {exePath}.");
                VerifyChecksum(http, assets.Checksum, exePath, workDir);

                var psi = new ProcessStartInfo(exePath, "--install")
                {
                    UseShellExecute = false,
                    // Saída redirecionada para pipes próprios: sem isso o launcher
                    // aberto herdaria o stdout de quem chamou o setup e a chamada
                    // só terminaria quando ele fosse fechado.
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = workDir
                };
                using Process? process = Process.Start(psi)
                    ?? throw new InvalidOperationException("Nao foi possivel iniciar o instalador.");
                return WaitForInstaller(process, InstallTarget(), TimeSpan.FromMinutes(3));
            }

            // Release de transicao (so zip, sem exe unico): extrai e pede a instalacao
            // a partir da pasta, que e o mesmo caminho usado pelo exe unico.
            AssetPick zipAssets = ReleasePicker.PickLauncherZip(latest.Assets, latest.Version);
            if (zipAssets.Executable is null)
            {
                throw new InvalidOperationException(
                    $"A release {latest.Version} nao tem exe unico nem zip do launcher.");
            }

            string zipPath = Path.Combine(workDir, zipAssets.Executable.Name);
            Download(http, zipAssets.Executable.Url, zipPath);
            Log.Write($"Baixado o zip em {zipPath}.");
            VerifyChecksum(http, zipAssets.Checksum, zipPath, workDir);

            string payloadDir = Path.Combine(workDir, "payload");
            string launcher = PayloadArchive.ExtractAndFind(zipPath, payloadDir)
                ?? throw new InvalidOperationException("O zip da release nao tem o executavel do launcher.");

            var folderPsi = new ProcessStartInfo(launcher, $"--install --from \"{payloadDir}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = payloadDir
            };
            using Process? folderProcess = Process.Start(folderPsi)
                ?? throw new InvalidOperationException("Nao foi possivel iniciar a instalacao a partir do zip.");
            return WaitForInstaller(folderProcess, InstallTarget(), TimeSpan.FromMinutes(3));
        }
        catch (Exception ex)
        {
            Log.Write("ERRO: " + ex.Message);
            if (!silent)
            {
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine("Detalhes em %LOCALAPPDATA%\\Ribanense Soluções\\setup.log");
            }
            return 1;
        }
    }

    internal static void Download(HttpClient http, string url, string destination)
    {
        using var response = http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
        using Stream source = response.Content.ReadAsStream();
        using FileStream target = File.Create(destination);
        source.CopyTo(target);
    }

    /// <summary>
    /// Espera o instalador concluir. Não espera a janela fechar: versões antigas do
    /// launcher deixam o processo aberto após instalar, e o setup deve terminar
    /// assim que a cópia instalada estiver no disco.
    /// </summary>
    internal static int WaitForInstaller(Process process, string installDir, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (process.HasExited)
            {
                string err = string.Empty;
                try { err = process.StandardError.ReadToEnd().Trim(); } catch { }
                Log.Write($"Instalador terminou com codigo {process.ExitCode}.");
                if (process.ExitCode != 0 && err.Length > 0) Log.Write("stderr: " + err);
                return process.ExitCode;
            }

            if (InstallationReady(installDir))
            {
                Log.Write("Copia instalada detectada; o setup terminou.");
                return 0;
            }

            Thread.Sleep(400);
        }

        Log.Write("Instalador seguiu em segundo plano (limite de espera atingido).");
        return 0;
    }

    /// <summary>
    /// A cópia pode aparecer na pasta pedida ou na padrão: versões publicadas antes
    /// do suporte a <c>--from</c> instalam sempre na pasta padrão.
    /// </summary>
    internal static bool InstallationReady(string installDir)
    {
        if (IsInstalled(installDir)) return true;
        string standard = DefaultInstallDirectory;
        return !string.Equals(standard, installDir, StringComparison.OrdinalIgnoreCase) &&
            IsInstalled(standard);
    }

    private static bool IsInstalled(string installDir) =>
        !string.IsNullOrWhiteSpace(installDir) &&
        File.Exists(Path.Combine(installDir, "Ribanense.Solucoes.Launcher.exe")) &&
        File.Exists(Path.Combine(installDir, "payload-manifest.json"));

    internal static string DefaultInstallDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            AppPaths.ProductFolderName);

    /// <summary>Pasta onde a instalação deve aparecer (respeita RIBANENSE_INSTALL_DIR).</summary>
    internal static string InstallTarget()
    {
        string? overrideDir = Environment.GetEnvironmentVariable("RIBANENSE_INSTALL_DIR");
        return string.IsNullOrWhiteSpace(overrideDir) ? DefaultInstallDirectory : overrideDir;
    }

    internal static void VerifyChecksum(HttpClient http, ReleaseAssetRef? checksum, string filePath, string workDir)
    {
        if (checksum is null)
        {
            Log.Write("Release sem .sha256: seguindo sem conferir hash.");
            return;
        }

        string shaPath = Path.Combine(workDir, checksum.Name);
        Download(http, checksum.Url, shaPath);
        string expected = Sha256Text.Extract(File.ReadAllText(shaPath))
            ?? throw new InvalidOperationException("O .sha256 da release nao tem hash valido.");
        string actual = Sha256Text.OfFile(filePath);
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"O arquivo baixado nao corresponde ao SHA256 publicado ({Path.GetFileName(filePath)}).");
        }

        Log.Write($"SHA256 conferido: {Path.GetFileName(filePath)}.");
    }
}

internal static class PayloadArchive
{
    internal static string? ExtractAndFind(string zipPath, string destination)
    {
        if (Directory.Exists(destination)) Directory.Delete(destination, true);
        Directory.CreateDirectory(destination);
        System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, destination, overwriteFiles: true);
        return FindEntry(destination);
    }

    internal static string? FindEntry(string root)
    {
        const string name = "Ribanense.Solucoes.Launcher.exe";
        if (!Directory.Exists(root)) return null;

        string direct = Path.Combine(root, name);
        if (File.Exists(direct)) return direct;

        string manifest = Path.Combine(root, "payload-manifest.json");
        if (File.Exists(manifest))
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(manifest));
                if (doc.RootElement.TryGetProperty("entryExecutable", out JsonElement entry))
                {
                    string? relative = entry.GetString();
                    if (!string.IsNullOrWhiteSpace(relative))
                    {
                        string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                        if (File.Exists(path)) return path;
                    }
                }
            }
            catch
            {
                // cai na busca
            }
        }

        return Directory.EnumerateFiles(root, name, SearchOption.AllDirectories).FirstOrDefault();
    }
}

internal sealed record LatestRelease(string Version, IReadOnlyList<ReleaseAssetRef> Assets);

internal sealed record ReleaseAssetRef(string Name, string Url);

internal sealed record AssetPick(ReleaseAssetRef? Executable, ReleaseAssetRef? Checksum);

internal static class ReleasePicker
{
    /// <summary>Maior SemVer entre as releases não-draft com o prefixo de tag.</summary>
    internal static LatestRelease? PickLatest(string json, string tagPrefix)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        using JsonDocument doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;

        string? bestVersion = null;
        IReadOnlyList<ReleaseAssetRef> bestAssets = Array.Empty<ReleaseAssetRef>();

        foreach (JsonElement release in doc.RootElement.EnumerateArray())
        {
            if (release.TryGetProperty("draft", out JsonElement draft) && draft.GetBoolean()) continue;
            if (release.TryGetProperty("prerelease", out JsonElement pre) && pre.GetBoolean()) continue;
            if (!release.TryGetProperty("tag_name", out JsonElement tagEl)) continue;

            string tag = tagEl.GetString() ?? string.Empty;
            if (!tag.StartsWith(tagPrefix, StringComparison.Ordinal)) continue;
            string version = tag[tagPrefix.Length..];
            if (!PluginSDK.Manifest.SemVerLoose.IsValid(version)) continue;

            if (bestVersion is not null &&
                PluginSDK.Manifest.SemVerLoose.Compare(version, bestVersion) <= 0)
            {
                continue;
            }

            bestVersion = version;
            bestAssets = ReadAssets(release);
        }

        return bestVersion is null ? null : new LatestRelease(bestVersion, bestAssets);
    }

    internal static AssetPick PickLauncherAssets(IReadOnlyList<ReleaseAssetRef> assets, string version)
    {
        string exeName = $"launcher-{version}-win-x64.exe";
        AssetPick pick = PickLauncherAssets(assets);
        if (pick.Executable is not null) return pick;

        ReleaseAssetRef? exact = assets.FirstOrDefault(a =>
            string.Equals(a.Name, exeName, StringComparison.OrdinalIgnoreCase));
        ReleaseAssetRef? exactSha = assets.FirstOrDefault(a =>
            string.Equals(a.Name, exeName + ".sha256", StringComparison.OrdinalIgnoreCase));
        return new AssetPick(exact, exactSha);
    }

    /// <summary>Exe único (não o .sha256) e o .sha256 dele.</summary>
    internal static AssetPick PickLauncherAssets(IReadOnlyList<ReleaseAssetRef> assets)
    {
        ReleaseAssetRef? exe = assets.FirstOrDefault(a =>
            a.Name.StartsWith("launcher-", StringComparison.OrdinalIgnoreCase) &&
            a.Name.EndsWith("-win-x64.exe", StringComparison.OrdinalIgnoreCase));
        if (exe is null) return new AssetPick(null, null);

        ReleaseAssetRef? sha = assets.FirstOrDefault(a =>
            string.Equals(a.Name, exe.Name + ".sha256", StringComparison.OrdinalIgnoreCase));
        return new AssetPick(exe, sha);
    }

    /// <summary>Zip da pasta do launcher, para releases que ainda não publicam o exe único.</summary>
    internal static AssetPick PickLauncherZip(IReadOnlyList<ReleaseAssetRef> assets, string version)
    {
        string zipName = $"launcher-{version}-win-x64.zip";
        ReleaseAssetRef? zip = assets.FirstOrDefault(a =>
                string.Equals(a.Name, zipName, StringComparison.OrdinalIgnoreCase))
            ?? assets.FirstOrDefault(a =>
                a.Name.StartsWith("launcher-", StringComparison.OrdinalIgnoreCase) &&
                a.Name.EndsWith("-win-x64.zip", StringComparison.OrdinalIgnoreCase));
        if (zip is null) return new AssetPick(null, null);

        ReleaseAssetRef? sha = assets.FirstOrDefault(a =>
            string.Equals(a.Name, zip.Name + ".sha256", StringComparison.OrdinalIgnoreCase));
        return new AssetPick(zip, sha);
    }

    private static IReadOnlyList<ReleaseAssetRef> ReadAssets(JsonElement release)
    {
        var assets = new List<ReleaseAssetRef>();
        if (!release.TryGetProperty("assets", out JsonElement array) ||
            array.ValueKind != JsonValueKind.Array)
        {
            return assets;
        }

        foreach (JsonElement asset in array.EnumerateArray())
        {
            string name = asset.TryGetProperty("name", out JsonElement n) ? n.GetString() ?? "" : "";
            string url = asset.TryGetProperty("browser_download_url", out JsonElement u) ? u.GetString() ?? "" : "";
            if (name.Length > 0 && url.Length > 0) assets.Add(new ReleaseAssetRef(name, url));
        }

        return assets;
    }
}

internal static class Sha256Text
{
    /// <summary>Primeiro token hexadecimal de 64 caracteres do arquivo .sha256.</summary>
    internal static string? Extract(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        foreach (string raw in text.Split(new[] { ' ', '\t', '\r', '\n', '*' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (raw.Length != 64) continue;
            if (raw.Any(c => !Uri.IsHexDigit(c))) continue;
            return raw.ToLowerInvariant();
        }

        return null;
    }

    internal static string OfFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)).ToLowerInvariant();
    }
}

internal static class Log
{
    private static string? _path;

    internal static void Init(string path)
    {
        _path = path;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        }
        catch
        {
            _path = null;
        }
    }

    internal static void Write(string message)
    {
        if (_path is null) return;
        try
        {
            File.AppendAllText(_path, $"[{DateTime.Now:O}] {message}{Environment.NewLine}");
        }
        catch
        {
            // log é auxiliar
        }
    }
}
