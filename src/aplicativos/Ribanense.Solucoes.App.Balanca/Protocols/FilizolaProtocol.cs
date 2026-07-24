using System.IO.Ports;
using Ribanense.Solucoes.App.Balanca.Domain;

namespace Ribanense.Solucoes.App.Balanca.Protocols;

/// <summary>
/// Protocolo Filizola. Solicita o peso com ENQ e recebe frame STX/ETX com o peso
/// em quilogramas (3 casas). Marcadores de instabilidade/negativo/sobrecarga.
/// </summary>
public sealed class FilizolaProtocol : DelimitedWeightProtocol
{
    public override string Key => "filizola";
    public override string DisplayName => "Filizola";

    public override SerialConfig DefaultConfig(string port) =>
        Config(port, 9600, 8, Parity.None, StopBits.One);
}
