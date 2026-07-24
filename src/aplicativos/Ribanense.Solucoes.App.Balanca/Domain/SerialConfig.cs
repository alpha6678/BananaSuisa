using System.IO.Ports;

namespace Ribanense.Solucoes.App.Balanca.Domain;

/// <summary>
/// Configuração de linha serial para abrir uma porta COM/USB-serial.
/// Espelha os parâmetros configuráveis na interface original do ACBrBAL.
/// </summary>
public sealed record SerialConfig(
    string Port,
    int BaudRate,
    int DataBits,
    Parity Parity,
    StopBits StopBits,
    Handshake Handshake,
    int TimeoutMs = 2000)
{
    public static SerialConfig Default(string port = "COM1") =>
        new(port, 9600, 8, Parity.None, StopBits.One, Handshake.None);

    /// <summary>Representação curta para exibição/log, ex.: "COM3 9600 8N1".</summary>
    public string ShortDescription =>
        $"{Port} {BaudRate} {DataBits}{ParityCode}{StopBitsCode}{HandshakeSuffix}";

    private string ParityCode => Parity switch
    {
        Parity.None => "N",
        Parity.Odd => "O",
        Parity.Even => "E",
        Parity.Mark => "M",
        Parity.Space => "S",
        _ => "?",
    };

    private string StopBitsCode => StopBits switch
    {
        StopBits.One => "1",
        StopBits.OnePointFive => "1.5",
        StopBits.Two => "2",
        _ => "?",
    };

    private string HandshakeSuffix => Handshake switch
    {
        Handshake.None => "",
        Handshake.XOnXOff => " XON/XOFF",
        Handshake.RequestToSend => " RTS/CTS",
        Handshake.RequestToSendXOnXOff => " RTS+XON",
        _ => "",
    };
}
