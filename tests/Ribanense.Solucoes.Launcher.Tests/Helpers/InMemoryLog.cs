using Ribanense.Solucoes.PluginSDK.Logging;

namespace Ribanense.Solucoes.Launcher.Tests.Helpers;

public sealed class InMemoryLog : IAppJsonLog
{
    public List<(AppLogLevel Level, string Category, string Message, Exception? Ex)> Entries { get; } = new();

    public void Write(AppLogLevel level, string category, string message, Exception? exception = null, IDictionary<string, string>? data = null)
    {
        Entries.Add((level, category, message, exception));
    }
}
