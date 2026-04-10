using BananaSuisa.Core.Logging;

namespace BananaSuisa.Services.Abstractions;

public interface IAppJsonLog
{
    string LogFilePath { get; }

    void Write(
        AppLogLevel level,
        string category,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, string>? data = null);
}
