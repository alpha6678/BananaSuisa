namespace Ribanense.Solucoes.App.Balanca.Protocols;

/// <summary>Caracteres de controle ASCII usados pelos protocolos de balança.</summary>
public static class SerialControl
{
    public const byte STX = 0x02;
    public const byte ETX = 0x03;
    public const byte ENQ = 0x05;
    public const byte ACK = 0x06;
    public const byte CR = 0x0D;
    public const byte LF = 0x0A;
}
