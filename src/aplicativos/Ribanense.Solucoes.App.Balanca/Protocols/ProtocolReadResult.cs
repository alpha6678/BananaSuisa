using Ribanense.Solucoes.App.Balanca.Domain;

namespace Ribanense.Solucoes.App.Balanca.Protocols;

/// <summary>Situação de uma passada de parsing incremental sobre o buffer recebido.</summary>
public enum FrameParseStatus
{
    /// <summary>O buffer ainda não contém um frame completo; continue acumulando.</summary>
    NeedMoreData,

    /// <summary>Um frame foi reconhecido e interpretado.</summary>
    FrameParsed,

    /// <summary>Os bytes iniciais são ruído/lixo e devem ser descartados para ressincronizar.</summary>
    InvalidData,
}

/// <summary>
/// Confiança de que o frame reconhecido corresponde de fato ao protocolo esperado.
/// Frames delimitados (STX/ETX) valem mais que texto solto salvo por heurística.
/// </summary>
public enum FrameConfidence
{
    None = 0,
    Low = 1,
    High = 2,
}

/// <summary>
/// Resultado de <see cref="IBalancaProtocol.Read"/>. Comunica ao leitor quantos bytes
/// consumir da frente do buffer e, quando aplicável, a leitura obtida.
/// </summary>
public sealed record ProtocolReadResult(
    FrameParseStatus Status,
    int Consumed,
    WeightReading? Reading,
    FrameConfidence Confidence)
{
    public static ProtocolReadResult NeedMore(int consumed = 0) =>
        new(FrameParseStatus.NeedMoreData, Math.Max(0, consumed), null, FrameConfidence.None);

    public static ProtocolReadResult Invalid(int consumed) =>
        new(FrameParseStatus.InvalidData, Math.Max(0, consumed), null, FrameConfidence.None);

    public static ProtocolReadResult Parsed(int consumed, WeightReading reading, FrameConfidence confidence) =>
        new(FrameParseStatus.FrameParsed, Math.Max(0, consumed), reading, confidence);
}
