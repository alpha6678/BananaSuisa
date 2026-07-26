using System.IO.Ports;
using Ribanense.Solucoes.App.Balanca.Domain;

namespace Ribanense.Solucoes.App.Balanca.Protocols;

/// <summary>
/// Protocolo Toledo 2180. Diferente da linha Prix: a resposta ao ENQ (0x05) não é
/// delimitada por STX/ETX. Cada quadro é terminado por CR (0x0D) e o peso vem após o
/// marcador <c>0x60</c> (`) com 6 dígitos sem ponto decimal (3 casas implícitas).
/// </summary>
/// <remarks>
/// Limitação conhecida: o firmware original só considera o peso estável quando três
/// quadros consecutivos coincidem. Aqui reconhecemos um quadro por vez (com marcador =
/// estável, alta confiança). Quadros sem marcador são tratados como ruído e descartados,
/// para não gerar falsos positivos na varredura. A confirmação de estabilidade por
/// múltiplos quadros fica pendente de validação com hardware real.
/// </remarks>
public sealed class Toledo2180Protocol : IBalancaProtocol
{
    private const byte Marker = 0x60;
    private const int WeightDigits = 6;
    private const int ImpliedDecimals = 3;

    public string Key => "toledo2180";
    public string DisplayName => "Toledo 2180";

    public byte[] BuildRequest() => new[] { SerialControl.ENQ };

    public SerialConfig DefaultConfig(string port) =>
        new(port, 9600, 8, Parity.None, StopBits.One, Handshake.None);

    public ProtocolReadResult Read(ReadOnlySpan<byte> buffer, bool isFinal)
    {
        var loc = WeightFrameParser.LocateLineFrame(buffer, isFinal);
        switch (loc.Status)
        {
            case WeightFrameParser.LocateStatus.NeedMoreData:
                return ProtocolReadResult.NeedMore(loc.Consumed);
            case WeightFrameParser.LocateStatus.Invalid:
                return ProtocolReadResult.Invalid(loc.Consumed);
        }

        var payload = loc.Payload;
        var framed = buffer[..loc.Consumed];
        string rawAscii = WeightFrameParser.ToAscii(framed).Trim();
        string rawHex = WeightFrameParser.ToHex(framed);

        int marker = payload.IndexOf(Marker);
        if (marker < 0 || marker + 1 + WeightDigits > payload.Length)
        {
            // Quadro sem o marcador esperado: trata como ruído e ressincroniza.
            return ProtocolReadResult.Invalid(loc.Consumed);
        }

        string digits = WeightFrameParser.ToAscii(payload.Slice(marker + 1, WeightDigits));
        if (!WeightFrameParser.TryParseImplicit(digits, ImpliedDecimals, out decimal weight))
            return ProtocolReadResult.Invalid(loc.Consumed);

        var reading = new WeightReading(WeightStatus.Estavel, weight, "kg", rawAscii, rawHex, HasWeight: true);
        return ProtocolReadResult.Parsed(loc.Consumed, reading, FrameConfidence.High);
    }
}
