using System.IO.Ports;
using Ribanense.Solucoes.App.Balanca.Domain;

namespace Ribanense.Solucoes.App.Balanca.Protocols;

/// <summary>
/// Protocolo Filizola (compatível com Toledo Prix3 prt3, protocolo P1). O host envia
/// ENQ (0x05) e a balança responde com <c>STX PPPPP ETX</c>. Peso estável, instável
/// (<c>IIIII</c>), prato aliviado/negativo (<c>NNNNN</c>) e sobrecarga (<c>SSSSS</c>).
/// Manuais Filizola/Micheletti: 8 data bits, 1 stop bit, sem paridade, 9600 bps.
/// </summary>
public sealed class FilizolaProtocol : DelimitedWeightProtocol
{
    public override string Key => "filizola";
    public override string DisplayName => "Filizola";

    public override SerialConfig DefaultConfig(string port) =>
        Config(port, 9600, 8, Parity.None, StopBits.One);
}
