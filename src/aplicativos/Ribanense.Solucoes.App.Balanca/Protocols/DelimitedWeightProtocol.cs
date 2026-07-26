using System.IO.Ports;
using Ribanense.Solucoes.App.Balanca.Domain;

namespace Ribanense.Solucoes.App.Balanca.Protocols;

/// <summary>
/// Base para protocolos que solicitam o peso com ENQ e recebem um frame delimitado
/// por STX/ETX (ou terminado por CR). Interpreta ponto decimal explícito quando
/// presente ou aplica casas decimais implícitas. Reconhece os tokens de status
/// (IIIII/NNNNN/SSSSS) como respostas válidas sem valor numérico.
/// </summary>
public abstract class DelimitedWeightProtocol : IBalancaProtocol
{
    public abstract string Key { get; }
    public abstract string DisplayName { get; }

    /// <summary>Casas decimais assumidas quando o frame não traz ponto decimal.</summary>
    protected virtual int ImpliedDecimals => 3;

    /// <summary>Unidade reportada quando o frame não a informa.</summary>
    protected virtual string DefaultUnit => "kg";

    public virtual byte[] BuildRequest() => new[] { SerialControl.ENQ };

    public abstract SerialConfig DefaultConfig(string port);

    public virtual ProtocolReadResult Read(ReadOnlySpan<byte> buffer, bool isFinal)
    {
        var loc = WeightFrameParser.LocateStxFrame(buffer, isFinal);
        return loc.Status switch
        {
            WeightFrameParser.LocateStatus.NeedMoreData => ProtocolReadResult.NeedMore(loc.Consumed),
            WeightFrameParser.LocateStatus.Invalid => ProtocolReadResult.Invalid(loc.Consumed),
            _ => BuildFrameReading(
                WeightFrameParser.ToAscii(loc.Payload).Trim(),
                buffer[..loc.Consumed],
                loc.Consumed,
                loc.Delimited,
                unit: null),
        };
    }

    /// <summary>
    /// Constrói a leitura a partir do payload textual já extraído. Reutilizado pelos
    /// protocolos específicos e pelo genérico (que fornece a unidade detectada).
    /// </summary>
    protected ProtocolReadResult BuildFrameReading(
        string payloadAscii,
        ReadOnlySpan<byte> framed,
        int consumed,
        bool delimited,
        string? unit)
    {
        if (payloadAscii.Length == 0)
            return ProtocolReadResult.Invalid(consumed);

        string rawAscii = WeightFrameParser.ToAscii(framed).Trim();
        string rawHex = WeightFrameParser.ToHex(framed);
        string effectiveUnit = unit ?? DefaultUnit;

        var status = WeightFrameParser.DetectFrameStatus(payloadAscii);
        var confidence = delimited ? FrameConfidence.High : FrameConfidence.Low;

        decimal weight = 0m;
        bool hasWeight = false;
        if (WeightFrameParser.TryParseExplicitDecimal(payloadAscii, out decimal explicitValue))
        {
            weight = Math.Abs(explicitValue);
            hasWeight = true;
        }
        else if (WeightFrameParser.TryParseImplicit(payloadAscii, ImpliedDecimals, out decimal implicitValue))
        {
            weight = implicitValue;
            hasWeight = true;
        }

        if (!hasWeight)
        {
            // Frame estável precisa de número; sem número é conteúdo inválido → ressincroniza.
            if (status is WeightStatus.Estavel or WeightStatus.NaoLido)
                return ProtocolReadResult.Invalid(consumed);

            // Status sem valor (IIIII/NNNNN/SSSSS): resposta válida, porém sem peso.
            var statusReading = WeightReading.StatusOnly(status, effectiveUnit, rawAscii, rawHex);
            return ProtocolReadResult.Parsed(consumed, statusReading, confidence);
        }

        if (status == WeightStatus.Negativo) weight = -Math.Abs(weight);

        var reading = new WeightReading(status, weight, effectiveUnit, rawAscii, rawHex, HasWeight: true);
        return ProtocolReadResult.Parsed(consumed, reading, confidence);
    }

    protected static SerialConfig Config(string port, int baud, int dataBits, Parity parity, StopBits stopBits) =>
        new(port, baud, dataBits, parity, stopBits, Handshake.None);
}
