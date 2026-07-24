using System.IO.Ports;
using Ribanense.Solucoes.App.Balanca.Domain;

namespace Ribanense.Solucoes.App.Balanca.Protocols;

/// <summary>
/// Protocolo Urano. Solicita o peso com ENQ e recebe frame STX/ETX com o peso
/// em quilogramas (3 casas).
/// </summary>
public sealed class UranoProtocol : DelimitedWeightProtocol
{
    public override string Key => "urano";
    public override string DisplayName => "Urano";

    public override SerialConfig DefaultConfig(string port) =>
        Config(port, 9600, 8, Parity.None, StopBits.One);
}
