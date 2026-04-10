namespace BananaSuisa.Core.Logging;

/// <summary>
/// Uma linha de diagnostico gravada como um objeto JSON (ficheiro em formato NDJSON).
/// </summary>
public sealed record JsonLogEntry(
    Guid SessionId,
    string AppVersion,
    int ProcessId,
    int ManagedThreadId,
    DateTime TimestampUtc,
    string Level,
    string Category,
    string Message,
    string? Exception,
    IReadOnlyDictionary<string, string>? Data);
