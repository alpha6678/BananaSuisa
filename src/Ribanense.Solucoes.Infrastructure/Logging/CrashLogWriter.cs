using System.IO;

namespace Ribanense.Solucoes.Infrastructure.Logging;

/// <summary>
/// Escreve crashes e mensagens críticas em arquivo de texto plano em
/// <c>%LOCALAPPDATA%\Ribanense Soluções\crash.log</c>, acessível sem
/// ferramentas externas. Todos os métodos engolem exceções internas
/// (o log de crash nunca deve derrubar o app).
/// </summary>
public static class CrashLogWriter
{
    public const string ProductFolderName = "Ribanense Soluções";
    public const string DefaultFileName = "crash.log";
    public const int MaxBytes = 1_000_000; // 1 MB → faz rotate para crash.old.log

    public static string DefaultPath
    {
        get
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, ProductFolderName, DefaultFileName);
        }
    }

    public static void Write(string component, Exception exception, string? overridePath = null)
    {
        if (exception is null) return;

        string block = $"[{DateTimeOffset.Now:O}] [{component}] {exception.GetType().FullName}: {exception.Message}"
            + Environment.NewLine
            + exception
            + Environment.NewLine
            + Environment.NewLine;

        WriteRaw(block, overridePath);
    }

    public static void Write(string component, string message, string? overridePath = null)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        string block = $"[{DateTimeOffset.Now:O}] [{component}] {message}" + Environment.NewLine;
        WriteRaw(block, overridePath);
    }

    private static void WriteRaw(string block, string? overridePath)
    {
        try
        {
            string path = overridePath ?? DefaultPath;
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            RotateIfNeeded(path);

            // FileMode.Append + FileShare.ReadWrite evita colisão entre
            // processos (Launcher + apps podem gravar no mesmo arquivo).
            using var fs = new FileStream(
                path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite);
            using var writer = new StreamWriter(fs);
            writer.Write(block);
        }
        catch
        {
            // best effort — este log nunca pode lançar.
        }
    }

    private static void RotateIfNeeded(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            var info = new FileInfo(path);
            if (info.Length < MaxBytes) return;

            string backup = Path.ChangeExtension(path, ".old.log");
            if (File.Exists(backup)) File.Delete(backup);
            File.Move(path, backup);
        }
        catch
        {
            // best effort
        }
    }
}
