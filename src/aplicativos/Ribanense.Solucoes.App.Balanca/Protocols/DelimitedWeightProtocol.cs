using System.IO.Ports;
using Ribanense.Solucoes.App.Balanca.Domain;

namespace Ribanense.Solucoes.App.Balanca.Protocols;

/// <summary>
/// Base para protocolos que solicitam o peso com ENQ e recebem um frame
/// delimitado por STX/ETX (ou terminado por CR). Interpreta ponto decimal
/// explícito quando presente ou aplica casas decimais implícitas.
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

    public virtual bool TryParse(ReadOnlySpan<byte> buffer, out WeightReading reading)
    {
        reading = WeightReading.NotRead();
        if (!WeightFrameParser.TryExtractPayload(buffer, out var payload))
            return false;

        string ascii = WeightFrameParser.ToAscii(payload).Trim();
        if (ascii.Length == 0) return false;

        string rawAscii = WeightFrameParser.ToAscii(buffer).Trim();
        string rawHex = WeightFrameParser.ToHex(buffer);

        var status = WeightFrameParser.DetectStatus(ascii);

        decimal weight;
        if (WeightFrameParser.TryParseExplicitDecimal(ascii, out decimal explicitValue))
        {
            weight = Math.Abs(explicitValue);
        }
        else if (WeightFrameParser.TryParseImplicit(ascii, ImpliedDecimals, out decimal implicitValue))
        {
            weight = implicitValue;
        }
        else
        {
            return false;
        }

        if (status == WeightStatus.Negativo) weight = -Math.Abs(weight);

        reading = new WeightReading(status, weight, DefaultUnit, rawAscii, rawHex);
        return true;
    }

    protected static SerialConfig Config(string port, int baud, int dataBits, Parity parity, StopBits stopBits) =>
        new(port, baud, dataBits, parity, stopBits, Handshake.None);
}
