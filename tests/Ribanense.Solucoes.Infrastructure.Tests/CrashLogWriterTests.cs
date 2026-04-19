using Ribanense.Solucoes.Infrastructure.Logging;
using Xunit;

namespace Ribanense.Solucoes.Infrastructure.Tests;

public class CrashLogWriterTests
{
    private static string NewTempPath()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ribanense-crashlog-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "crash.log");
    }

    [Fact]
    public void Write_exception_creates_file_with_type_and_message()
    {
        string path = NewTempPath();
        try
        {
            var ex = new InvalidOperationException("test failure");
            CrashLogWriter.Write("TestHarness", ex, path);

            Assert.True(File.Exists(path));
            string content = File.ReadAllText(path);
            Assert.Contains("TestHarness", content);
            Assert.Contains("InvalidOperationException", content);
            Assert.Contains("test failure", content);
        }
        finally
        {
            TryCleanup(path);
        }
    }

    [Fact]
    public void Write_message_appends_line()
    {
        string path = NewTempPath();
        try
        {
            CrashLogWriter.Write("Harness", "primeira linha", path);
            CrashLogWriter.Write("Harness", "segunda linha", path);

            string content = File.ReadAllText(path);
            Assert.Contains("primeira linha", content);
            Assert.Contains("segunda linha", content);
        }
        finally
        {
            TryCleanup(path);
        }
    }

    [Fact]
    public void Write_multiple_processes_can_coexist_via_fileshare()
    {
        // Simula dois "processos" escrevendo sequencialmente no mesmo arquivo.
        string path = NewTempPath();
        try
        {
            for (int i = 0; i < 50; i++)
            {
                CrashLogWriter.Write("A", $"linha-A-{i}", path);
                CrashLogWriter.Write("B", $"linha-B-{i}", path);
            }

            string content = File.ReadAllText(path);
            Assert.Contains("linha-A-0", content);
            Assert.Contains("linha-B-49", content);
        }
        finally
        {
            TryCleanup(path);
        }
    }

    [Fact]
    public void Write_null_exception_is_noop()
    {
        string path = NewTempPath();
        try
        {
            CrashLogWriter.Write("X", (Exception)null!, path);
            Assert.False(File.Exists(path));
        }
        finally
        {
            TryCleanup(path);
        }
    }

    [Fact]
    public void Write_invalid_path_does_not_throw()
    {
        // best-effort: caminho invalido nao pode matar o app
        CrashLogWriter.Write("X", new Exception("y"), overridePath: "Z:\\caminho\\invalido\\nao\\existe\\x.log");
        // se chegamos aqui sem exception, passou
        Assert.True(true);
    }

    private static void TryCleanup(string path)
    {
        try
        {
            string? dir = Path.GetDirectoryName(path);
            if (dir != null && Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        catch { }
    }
}
