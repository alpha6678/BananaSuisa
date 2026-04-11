using BananaSuisa.Core.Logging;
using BananaSuisa.Core.Versioning;
using BananaSuisa.Services.Abstractions;

namespace BananaSuisa.Infrastructure.Logging;

public sealed class AppJsonLogWriter : IAppJsonLog
{
    private static readonly Guid SessionId = Guid.NewGuid();
    private readonly IVault _vault;

    public AppJsonLogWriter(IVault vault)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
    }

    public string LogFilePath => "(vault)";

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

            _vault.WriteLog(entry);
        }
        catch
        {
        }
    }
}
