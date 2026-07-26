using System.Text;
using Ribanense.Solucoes.App.Balanca.Protocols;

namespace Ribanense.Solucoes.App.Balanca.Tests;

/// <summary>Monta frames seriais de exemplo (fixtures) para os testes de parsing.</summary>
internal static class FrameFactory
{
    /// <summary>Frame delimitado STX + corpo + ETX + CRLF (Toledo/Filizola).</summary>
    public static byte[] Delimited(string body)
    {
        var bytes = new List<byte> { SerialControl.STX };
        bytes.AddRange(Encoding.ASCII.GetBytes(body));
        bytes.Add(SerialControl.ETX);
        bytes.Add(SerialControl.CR);
        bytes.Add(SerialControl.LF);
        return bytes.ToArray();
    }

    /// <summary>Linha de texto terminada por CR (Urano/estilo texto).</summary>
    public static byte[] Line(string body)
    {
        var bytes = new List<byte>();
        bytes.AddRange(Encoding.ASCII.GetBytes(body));
        bytes.Add(SerialControl.CR);
        return bytes.ToArray();
    }

    /// <summary>Quadro Toledo 2180: prefixo + marcador 0x60 + 6 dígitos + CR.</summary>
    public static byte[] Toledo2180(string sixDigits, string prefix = "  ")
    {
        var bytes = new List<byte>();
        bytes.AddRange(Encoding.ASCII.GetBytes(prefix));
        bytes.Add(0x60);
        bytes.AddRange(Encoding.ASCII.GetBytes(sixDigits));
        bytes.Add(SerialControl.CR);
        return bytes.ToArray();
    }
}
