using System.Diagnostics;
using Ribanense.Solucoes.App.Farol.Domain;

namespace Ribanense.Solucoes.App.Farol.Collectors;

/// <summary>
/// Erros e avisos recentes de Application e System. Janela curta de propósito:
/// o objetivo é "o que quebrou agora", não auditoria histórica.
/// </summary>
public sealed class EventLogCollector : ICollector
{
    private const int MaxEntriesPerLog = 25;
    private static readonly string[] Logs = { "Application", "System" };

    private readonly TimeSpan _window;

    public EventLogCollector(TimeSpan? window = null)
    {
        _window = window ?? TimeSpan.FromHours(2);
    }

    public string Id => "eventlog";
    public string DisplayName => "Eventos do Windows";

    public Task CollectAsync(EvidenceBundleBuilder builder, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Event Log indisponível nesta plataforma.");

        DateTime cutoff = DateTime.Now - _window;

        foreach (string logName in Logs)
        {
            ct.ThrowIfCancellationRequested();
            builder.Events.AddRange(ReadLog(logName, cutoff, ct));
        }

        return Task.CompletedTask;
    }

    private static IEnumerable<EventEntryInfo> ReadLog(string logName, DateTime cutoff, CancellationToken ct)
    {
        var collected = new List<EventEntryInfo>();

        try
        {
            using var log = new EventLog(logName);

            // Percorre do mais recente para trás e para assim que cruzar a janela:
            // varrer o log inteiro em máquina antiga custa segundos.
            for (int i = log.Entries.Count - 1; i >= 0 && collected.Count < MaxEntriesPerLog; i--)
            {
                ct.ThrowIfCancellationRequested();

                EventLogEntry entry;
                try { entry = log.Entries[i]; }
                catch (ArgumentException) { continue; }

                if (entry.TimeGenerated < cutoff) break;

                if (entry.EntryType is not (EventLogEntryType.Error or EventLogEntryType.Warning))
                    continue;

                collected.Add(new EventEntryInfo(
                    LogName: logName,
                    Level: entry.EntryType == EventLogEntryType.Error ? "Erro" : "Aviso",
                    Source: entry.Source,
                    EventId: (int)(entry.InstanceId & 0xFFFF),
                    TimeGenerated: new DateTimeOffset(entry.TimeGenerated),
                    Message: Truncate(entry.Message, 400)));
            }
        }
        catch (System.Security.SecurityException ex)
        {
            throw new CollectorDeniedException($"Sem permissão para ler o log {logName}.", ex);
        }
        catch (InvalidOperationException)
        {
            // Log inexistente ou corrompido nesta máquina.
        }

        return collected;
    }

    /// <summary>
    /// Achata a mensagem em uma linha. Mensagens do Event Log vêm com quebras e
    /// indentação que quebram tanto a lista da UI quanto o JSON do dossiê.
    /// </summary>
    internal static string Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var single = new System.Text.StringBuilder(value.Length);
        bool lastWasSpace = false;

        foreach (char c in value)
        {
            bool isSpace = char.IsWhiteSpace(c);
            if (isSpace && lastWasSpace) continue;

            single.Append(isSpace ? ' ' : c);
            lastWasSpace = isSpace;
        }

        string flat = single.ToString().Trim();
        return flat.Length <= max ? flat : flat[..max] + "…";
    }
}
