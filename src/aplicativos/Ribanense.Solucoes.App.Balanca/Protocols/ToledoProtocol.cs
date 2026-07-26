using System.IO.Ports;
using Ribanense.Solucoes.App.Balanca.Domain;

namespace Ribanense.Solucoes.App.Balanca.Protocols;

/// <summary>
/// Protocolo Toledo (linha Prix/9094 e compatíveis, protocolo P05A/Prt3). O host envia
/// ENQ (0x05) e a balança responde com <c>STX PPPPP ETX</c>, peso sem ponto decimal
/// (3 casas implícitas). Instável/negativo/sobrecarga viram <c>IIIII</c>/<c>NNNNN</c>/<c>SSSSS</c>.
/// Manuais Toledo do Brasil: 8 data bits, 1 stop bit, sem paridade; baud 2400/4800/9600.
/// </summary>
public sealed class ToledoProtocol : DelimitedWeightProtocol
{
    public override string Key => "toledo";
    public override string DisplayName => "Toledo";

    public override SerialConfig DefaultConfig(string port) =>
        Config(port, 9600, 8, Parity.None, StopBits.One);
}
