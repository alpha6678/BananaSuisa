using Ribanense.Solucoes.App.Balanca.Domain;
using Ribanense.Solucoes.App.Balanca.Protocols;

namespace Ribanense.Solucoes.App.Balanca.Services;

/// <summary>Telemetria de uma tentativa de leitura, para log e diagnóstico.</summary>
public sealed record SerialReadDiagnostics(
    int BytesSent,
    int BytesReceived,
    long? MillisToFirstByte,
    long TotalMillis,
    bool FrameRecognized,
    string Reason,
    string RawHex)
{
    /// <summary>Linha compacta para log (raw hex fica opt-in via <see cref="RawHex"/>).</summary>
    public string Summary =>
        $"tx={BytesSent}B rx={BytesReceived}B " +
        $"1ºbyte={(MillisToFirstByte is { } f ? f + "ms" : "—")} total={TotalMillis}ms " +
        (FrameRecognized ? "frame ok" : $"sem frame ({Reason})");
}

/// <summary>Resultado completo de uma leitura: peso, confiança do frame e diagnóstico.</summary>
public sealed record SerialReadOutcome(
    WeightReading Reading,
    FrameConfidence Confidence,
    SerialReadDiagnostics Diagnostics);
