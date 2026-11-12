using System.Diagnostics;
using System.IO;
using Ribanense.Solucoes.Launcher.Configuration;

namespace Ribanense.Solucoes.Launcher.Services;

/// <summary>Ocupação em disco das pastas do launcher.</summary>
public sealed record LauncherUsage(
    int InstalledApps,
    long InstalledAppsBytes,
    int AppDataFolders,
    long AppDataBytes);

/// <summary>
/// Pastas do launcher e limpeza dos dados gravados por ele. As pastas ficam sob
/// <see cref="LauncherConfig.LauncherDataRoot"/> (instalado/avulso) ou dentro da
/// pasta da cópia portátil.
/// </summary>
public static class LauncherMaintenance
{
    /// <summary>Dados que cada app gravou, recebidos em <c>RIBANENSE_APP_DATA</c>.</summary>
    public static string AppDataRootFor(string launcherDataRoot) =>
        Path.Combine(launcherDataRoot, "apps");

    /// <summary>Contagem e tamanho das pastas de apps instalados e de dados.</summary>
    public static LauncherUsage Measure(string aplicativosRoot, string appDataRoot) =>
        new(
            CountDirectories(aplicativosRoot),
            SizeOf(aplicativosRoot),
            CountDirectories(appDataRoot),
            SizeOf(appDataRoot));

    /// <summary>
    /// Apaga o conteúdo de <paramref name="appDataRoot"/>. Os binários dos apps ficam;
    /// só os dados locais deles são removidos. Retorna quantas pastas saíram.
    /// </summary>
    public static int ClearAppData(string appDataRoot)
    {
        if (string.IsNullOrWhiteSpace(appDataRoot) || !Directory.Exists(appDataRoot)) return 0;

        int removed = 0;
        foreach (string dir in Directory.GetDirectories(appDataRoot))
        {
            try
            {
                Directory.Delete(dir, recursive: true);
                removed++;
            }
            catch
            {
                // app em execução ou arquivo travado: segue com os demais
            }
        }

        return removed;
    }

    /// <summary>Cria a pasta (se preciso) e abre no Explorador de Arquivos.</summary>
    public static void OpenInExplorer(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    /// <summary>
    /// Apaga a pasta depois que este processo encerrar. Necessário para pastas com
    /// arquivos em uso (o próprio executável, o Launcher.dat aberto pelo LiteDB).
    /// </summary>
    public static void ScheduleDelete(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        RunAfterExit($"rmdir /s /q \"{path.Replace("\"", string.Empty)}\"");
    }

    /// <summary>Apaga um único arquivo depois que este processo encerrar.</summary>
    public static void ScheduleDeleteFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        RunAfterExit($"del /f /q \"{path.Replace("\"", string.Empty)}\"");
    }

    public static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "0 KB";
        double kb = bytes / 1024.0;
        if (kb < 1024) return $"{kb:0} KB";
        double mb = kb / 1024.0;
        if (mb < 1024) return $"{mb:0.#} MB";
        return $"{mb / 1024.0:0.##} GB";
    }

    private static void RunAfterExit(string command)
    {
        string script = Path.Combine(
            Path.GetTempPath(), "ribanense-cleanup-" + Guid.NewGuid().ToString("N") + ".cmd");
        File.WriteAllText(script,
            "@echo off\r\n" +
            "timeout /t 2 /nobreak >nul\r\n" +
            command + "\r\n" +
            "del \"%~f0\"\r\n");
        Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + script + "\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }

    private static int CountDirectories(string path)
    {
        try
        {
            return Directory.Exists(path) ? Directory.GetDirectories(path).Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static long SizeOf(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return 0;

        long total = 0;
        try
        {
            foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try { total += new FileInfo(file).Length; } catch { }
            }
        }
        catch
        {
            // pasta removida no meio da varredura
        }

        return total;
    }
}
