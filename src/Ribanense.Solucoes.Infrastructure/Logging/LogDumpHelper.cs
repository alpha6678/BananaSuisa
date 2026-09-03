using System.IO;
using Ribanense.Solucoes.Infrastructure.Vault;
using Ribanense.Solucoes.PluginSDK.Logging;

namespace Ribanense.Solucoes.Infrastructure.Logging;

/// <summary>
/// Formata entradas do vault no stdout de forma legível. Usa uma cópia
/// temporária do arquivo .dat para não bloquear o processo dono do vault
/// (ex.: Launcher em execução enquanto o usuário roda <c>rb logs</c>).
/// </summary>
public static class LogDumpHelper
{
    public static int DumpToConsole(string vaultPath, int count, TextWriter? output = null, TextWriter? error = null)
    {
        var outWriter = output ?? Console.Out;
        var errWriter = error ?? Console.Error;

        if (!File.Exists(vaultPath))
        {
            errWriter.WriteLine($"Nenhum vault em: {vaultPath}");
            return 0;
        }

        string tmp = Path.Combine(Path.GetTempPath(), $"ribanense-log-{Guid.NewGuid():N}.dat");
        string tmpWal = SidecarLogPath(tmp);
        try
        {
            File.Copy(vaultPath, tmp, overwrite: true);

            // O LiteDB só faz checkpoint do .dat quando o dono fecha o banco. Um app
            // de bandeja fica dias aberto, então a maior parte dos registros ainda
            // está no -log.dat; sem copiá-lo junto o dump sai vazio.
            string wal = SidecarLogPath(vaultPath);
            if (File.Exists(wal)) File.Copy(wal, tmpWal, overwrite: true);

            using var vault = new LiteDbVault(tmp);
            var logs = vault.GetRecentLogs(count);

            if (logs.Count == 0)
            {
                outWriter.WriteLine($"(sem entradas em {vaultPath})");
                return 0;
            }

            foreach (var entry in logs)
            {
                WriteEntry(outWriter, entry);
            }

            outWriter.WriteLine();
            outWriter.WriteLine($"({logs.Count} entrada(s) de {vaultPath})");
            return 0;
        }
        catch (Exception ex)
        {
            errWriter.WriteLine($"Falha ao ler logs de {vaultPath}: {ex.Message}");
            return 1;
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            try { if (File.Exists(tmpWal)) File.Delete(tmpWal); } catch { }
        }
    }

    /// <summary>
    /// Caminho do log de escrita que o LiteDB mantém ao lado do banco:
    /// <c>Farol.dat</c> tem como par <c>Farol-log.dat</c>.
    /// </summary>
    internal static string SidecarLogPath(string vaultPath)
    {
        string? dir = Path.GetDirectoryName(vaultPath);
        string name = Path.GetFileNameWithoutExtension(vaultPath) + "-log" + Path.GetExtension(vaultPath);

        return string.IsNullOrEmpty(dir) ? name : Path.Combine(dir, name);
    }

    private static void WriteEntry(TextWriter writer, JsonLogEntry entry)
    {
        string time = entry.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff");
        writer.WriteLine($"[{time}] [{entry.Level,-11}] [{entry.Category,-24}] {entry.Message}");

        if (!string.IsNullOrEmpty(entry.Exception))
        {
            foreach (string line in entry.Exception.Split('\n'))
            {
                writer.WriteLine("    " + line.TrimEnd());
            }
        }

        if (entry.Data is not null && entry.Data.Count > 0)
        {
            foreach (var kv in entry.Data)
            {
                writer.WriteLine($"    {kv.Key}={kv.Value}");
            }
        }
    }
}
