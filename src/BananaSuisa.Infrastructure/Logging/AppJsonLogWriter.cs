using System.Text.Json;
using BananaSuisa.Core.Logging;
using BananaSuisa.Core.Versioning;
using BananaSuisa.Services.Abstractions;

namespace BananaSuisa.Infrastructure.Logging;

public sealed class AppJsonLogWriter : IAppJsonLog
{
    private static readonly Guid SessionId = Guid.NewGuid();
    private readonly object _lock = new();
    private readonly string _path;
    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public AppJsonLogWriter(string logFilePath)
    {
        _path = logFilePath ?? throw new ArgumentNullException(nameof(logFilePath));
        string? dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public string LogFilePath => _path;

    public void Write(
        AppLogLevel level,
        string category,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, string>? data = null)
    {
        try
        {
            var entry = new JsonLogEntry(
                SessionId,
                AppVersion.Value,
                Environment.ProcessId,
                Environment.CurrentManagedThreadId,
                DateTime.UtcNow,
                level.ToString(),
                category,
                message,
                exception?.ToString(),
                data);

            string line = JsonSerializer.Serialize(entry, _json) + Environment.NewLine;

            lock (_lock)
            {
                File.AppendAllText(_path, line);
            }
        }
        catch
        {
            // Diagnostico nao deve derrubar a aplicacao.
        }
    }
}
