namespace Ribanense.Solucoes.PluginSDK.Logging;

public interface IAppJsonLog
{
    void Write(
        AppLogLevel level,
        string category,
        string message,
        Exception? exception = null,
        IDictionary<string, string>? data = null);
}
