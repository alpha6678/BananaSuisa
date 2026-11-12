using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Ribanense.Solucoes.PluginSDK;
using Ribanense.Solucoes.PluginSDK.Payload;

namespace Ribanense.Solucoes.LauncherBootstrap;

/// <summary>
/// Exe único do launcher: leva a pasta do launcher embutida como <c>payload.zip</c>.
/// Extrai essa pasta para um cache por versão e abre o launcher de verdade de lá,
/// então rodar e instalar nunca baixam nada. Com a cópia já instalada, quem
/// atualiza é o próprio launcher, por delta.
/// </summary>
internal static class Program
{
    internal const string PayloadFileName = "payload.zip";
    internal const string LauncherExecutableName = "Ribanense.Solucoes.Launcher.exe";
    internal const string ReadyMarkerFileName = ".payload-ready";

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Any(a => a.Equals("--version", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine($"{{\"version\":\"{AppVersion.ForEntry()}\",\"sdk\":\"{SdkVersion.Current}\"}}");
                return 0;
            }

            string payload = Path.Combine(AppContext.BaseDirectory, PayloadFileName);
            string root = Path.Combine(CacheRoot(), AppVersion.ForEntry());
            string? launcher = PrepareRuntime(payload, root);
            if (launcher is null)
            {
                Console.Error.WriteLine(
                    "O pacote do launcher não veio neste arquivo.\n\n" +
                    $"Esperado em: {payload}\n" +
                    "Baixe o launcher de novo pela página de releases.");
                return 2;
            }

            if (!StartLauncher(launcher, args))
            {
                Console.Error.WriteLine("Não foi possível abrir o Ribanense Soluções.");
                return 1;
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    /// <summary>Pasta onde a versão desta cópia vive: <c>%LOCALAPPDATA%\Ribanense Soluções\runtime\&lt;versão&gt;</c>.</summary>
    internal static string CacheRoot() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppPaths.ProductFolderName,
            "runtime");

    /// <summary>
    /// Garante a extração desta versão e devolve o launcher pronto para abrir.
    /// Reaproveita a extração anterior quando o marcador indica que ela terminou.
    /// </summary>
    internal static string? PrepareRuntime(string payloadPath, string root)
    {
        string ready = Path.Combine(root, ReadyMarkerFileName);
        if (File.Exists(ready))
        {
            string? existing = FindLauncher(root);
            if (existing is not null) return existing;
        }

        if (!File.Exists(payloadPath)) return null;

        string staging = root + ".new-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            PayloadApplier.ExtractZip(File.ReadAllBytes(payloadPath), staging);
            File.WriteAllText(Path.Combine(staging, ReadyMarkerFileName), AppVersion.ForEntry());

            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            Directory.CreateDirectory(Path.GetDirectoryName(root)!);
            Directory.Move(staging, root);
        }
        catch
        {
            try
            {
                if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            }
            catch
            {
                // best effort
            }

            throw;
        }

        return FindLauncher(root);
    }

    internal static string? FindLauncher(string root)
    {
        if (!Directory.Exists(root)) return null;

        string direct = Path.Combine(root, LauncherExecutableName);
        if (File.Exists(direct)) return direct;

        string manifestPath = Path.Combine(root, PayloadManifest.FileName);
        if (File.Exists(manifestPath))
        {
            try
            {
                string entry = PayloadManifest.Load(manifestPath).EntryExecutable;
                if (!string.IsNullOrWhiteSpace(entry))
                {
                    string path = Path.Combine(root, entry.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(path)) return path;
                }
            }
            catch
            {
                // cai na busca abaixo
            }
        }

        return Directory.EnumerateFiles(root, LauncherExecutableName, SearchOption.AllDirectories).FirstOrDefault();
    }

    internal static bool StartLauncher(string launcherPath, IReadOnlyList<string> args)
    {
        var psi = new ProcessStartInfo(launcherPath, QuoteArguments(args))
        {
            // Saída em pipes próprios: com herança, o launcher aberto manteria o
            // stdout de quem chamou o exe único (rb, Explorer, setup) e a chamada
            // não terminaria até o usuário fechar o launcher.
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(launcherPath) ?? Environment.CurrentDirectory
        };

        try
        {
            using Process? process = Process.Start(psi);
            return process is not null;
        }
        catch
        {
            return false;
        }
    }

    internal static string QuoteArguments(IReadOnlyList<string> args)
    {
        if (args.Count == 0) return string.Empty;
        return string.Join(' ', args.Select(QuoteArgument));
    }

    private static string QuoteArgument(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        if (!value.Any(c => char.IsWhiteSpace(c) || c == '"')) return value;
        return "\"" + value.Replace("\\\"", "\\\"").Replace("\"", "\\\"") + "\"";
    }
}
