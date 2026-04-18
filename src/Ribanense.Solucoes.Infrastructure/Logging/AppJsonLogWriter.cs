using Ribanense.Solucoes.PluginSDK;
using Ribanense.Solucoes.PluginSDK.Logging;
using Ribanense.Solucoes.PluginSDK.Vault;

namespace Ribanense.Solucoes.Infrastructure.Logging;

/// <summary>
/// Implementação padrão de <see cref="IAppJsonLog"/> que persiste entradas
/// em um <see cref="IVault"/>. Cada instância gera uma SessionId única
/// correspondente à vida útil do objeto (tipicamente uma execução do app).
/// </summary>
public sealed class AppJsonLogWriter : IAppJsonLog
{
    private readonly IVault _vault;
    private readonly Guid _sessionId;
    private readonly string _appVersion;
    private readonly int _processId;

    public AppJsonLogWriter(IVault vault)
        : this(vault, Guid.NewGuid(), AppVersion.ForEntry(), Environment.ProcessId)
    {
    }

    public AppJsonLogWriter(IVault vault, Guid sessionId, string appVersion, int processId)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _sessionId = sessionId;
        _appVersion = appVersion;
        _processId = processId;
    }

    public Guid SessionId => _sessionId;

    public void Write(
        AppLogLevel level,
        string category,
        string message,
        Exception? exception = null,
        IDictionary<string, string>? data = null)
    {
        var entry = new JsonLogEntry(
            _sessionId,
            _appVersion,
            _processId,
            Environment.CurrentManagedThreadId,
            DateTime.UtcNow,
            level.ToString(),
            category ?? string.Empty,
            message ?? string.Empty,
            exception?.ToString(),
            data is null ? null : new Dictionary<string, string>(data));

        _vault.WriteLog(entry);
    }
}
