using System.Text;
using Ribanense.Solucoes.App.Balanca.Protocols;

namespace Ribanense.Solucoes.App.Balanca.Tests;

/// <summary>Monta frames seriais de exemplo para os testes de parsing.</summary>
internal static class FrameFactory
{
    public static byte[] Delimited(string body)
    {
        var bytes = new List<byte> { SerialControl.STX };
        bytes.AddRange(Encoding.ASCII.GetBytes(body));
        bytes.Add(SerialControl.ETX);
        bytes.Add(SerialControl.CR);
        bytes.Add(SerialControl.LF);
        return bytes.ToArray();
    }
}
