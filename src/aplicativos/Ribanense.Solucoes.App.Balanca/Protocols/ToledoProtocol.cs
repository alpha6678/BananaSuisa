using System.IO.Ports;
using Ribanense.Solucoes.App.Balanca.Domain;

namespace Ribanense.Solucoes.App.Balanca.Protocols;

/// <summary>
/// Protocolo Toledo (linha Prix e compatíveis). Solicita o peso com ENQ e recebe
/// um frame delimitado por STX/ETX com o peso em quilogramas (3 casas).
/// </summary>
public sealed class ToledoProtocol : DelimitedWeightProtocol
{
    public override string Key => "toledo";
    public override string DisplayName => "Toledo";

    public override SerialConfig DefaultConfig(string port) =>
        Config(port, 9600, 8, Parity.None, StopBits.One);
}
