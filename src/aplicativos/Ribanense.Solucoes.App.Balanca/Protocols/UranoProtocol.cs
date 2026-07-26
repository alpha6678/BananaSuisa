using System.IO.Ports;
using Ribanense.Solucoes.App.Balanca.Domain;

namespace Ribanense.Solucoes.App.Balanca.Protocols;

/// <summary>
/// Protocolo Urano. O host solicita o peso (manuais Urano indicam 0x04 ou 0x05; usamos
/// ENQ 0x05) e a balança responde em uma das formas conhecidas:
/// <list type="bullet">
/// <item>Frame delimitado por STX/ETX (linha "Urano 12" numérica);</item>
/// <item>Texto terminado por CR contendo algo como <c>PESO: 5,10kg</c>.</item>
/// </list>
/// Diferente das demais, a serial padrão documentada da Urano é <b>9600 8N2</b>
/// (dois stop bits), por isso o default abaixo não é 8N1.
/// </summary>
public sealed class UranoProtocol : DelimitedWeightProtocol
{
    public override string Key => "urano";
    public override string DisplayName => "Urano";

    public override SerialConfig DefaultConfig(string port) =>
        new(port, 9600, 8, Parity.None, StopBits.Two, Handshake.None);

    public override ProtocolReadResult Read(ReadOnlySpan<byte> buffer, bool isFinal)
    {
        // Forma 1: frame STX/ETX (reaproveita a base).
        if (buffer.IndexOf(SerialControl.STX) >= 0)
            return base.Read(buffer, isFinal);

        // Forma 2: texto terminado por CR/LF ("PESO: x,yz kg").
        var loc = WeightFrameParser.LocateLineFrame(buffer, isFinal);
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
}
