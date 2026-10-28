using System;
using System.Collections.Generic;

namespace Ribanense.Solucoes.App.Recuperacao.Recovery.Core;

/// <summary>Classificação de um evento emitido durante uma operação de recuperação.</summary>
public enum OperationEventKind
{
    Info,
    Ok,
    Warning,
    Error,
}

/// <summary>Evento único de progresso/log emitido por um serviço de recuperação.</summary>
public sealed record OperationEvent(OperationEventKind Kind, string Message);

/// <summary>Coletor de eventos que repassa tudo para um sink de log (thread-safe).</summary>
public sealed class OperationEventSink : IProgress<OperationEvent>
{
    private readonly Action<OperationEvent> _sink;

    public OperationEventSink(Action<OperationEvent> sink) =>
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));

    public void Report(OperationEvent item) => _sink(item);

    public static OperationEventSink FromText(Action<string> line) =>
        new(e => line(Format(e)));

    public static string Format(OperationEvent e) => e.Kind switch
    {
        OperationEventKind.Ok => $"[OK] {e.Message}",
        OperationEventKind.Warning => $"[AVISO] {e.Message}",
        OperationEventKind.Error => $"[ERRO] {e.Message}",
        _ => e.Message,
    };
}
