using System.IO.Ports;
using Ribanense.Solucoes.App.Balanca.Domain;

namespace Ribanense.Solucoes.App.Balanca.Protocols;

/// <summary>
/// Protocolo genérico/experimental. Não conhece a marca: solicita o peso com ENQ e
/// reconhece o peso por heurística. Dentro de um frame STX/ETX aceita número explícito
/// ou implícito (alta confiança). Sem frame, só aceita número decimal explícito na
/// tentativa final (baixa confiança), para não confundir ruído de linha com peso.
/// </summary>
public sealed class GenericHeuristicProtocol : DelimitedWeightProtocol
{
    public override string Key => "generico";
    public override string DisplayName => "Automático / Genérico";

    public override SerialConfig DefaultConfig(string port) =>
        Config(port, 9600, 8, Parity.None, StopBits.One);

    public override ProtocolReadResult Read(ReadOnlySpan<byte> buffer, bool isFinal)
    {
        if (buffer.IsEmpty)
            return isFinal ? ProtocolReadResult.Invalid(0) : ProtocolReadResult.NeedMore(0);

        // Caminho preferencial: frame delimitado por STX.
        if (buffer.IndexOf(SerialControl.STX) >= 0)
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
                    unit: WeightFrameParser.DetectUnit(WeightFrameParser.ToAscii(loc.Payload))),
            };
        }

        // Sem STX: aguarda mais dados; só decide no final.
        if (!isFinal)
            return ProtocolReadResult.NeedMore(0);

        string ascii = WeightFrameParser.ToAscii(buffer).Trim();
        if (WeightFrameParser.TryParseExplicitDecimal(ascii, out decimal value))
        {
            var status = WeightFrameParser.DetectFrameStatus(ascii);
            decimal weight = Math.Abs(value);
            if (status == WeightStatus.Negativo) weight = -weight;
            var reading = new WeightReading(status, weight, WeightFrameParser.DetectUnit(ascii),
                ascii, WeightFrameParser.ToHex(buffer), HasWeight: true);
            return ProtocolReadResult.Parsed(buffer.Length, reading, FrameConfidence.Low);
        }

        // Ruído sem número reconhecível: descarta.
        return ProtocolReadResult.Invalid(buffer.Length);
    }
}
