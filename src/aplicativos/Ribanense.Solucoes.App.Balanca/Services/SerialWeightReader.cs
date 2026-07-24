using System.Diagnostics;
using Ribanense.Solucoes.App.Balanca.Domain;
using Ribanense.Solucoes.App.Balanca.Protocols;
using Ribanense.Solucoes.App.Balanca.Serial;

namespace Ribanense.Solucoes.App.Balanca.Services;

/// <summary>
/// Núcleo de leitura de peso sobre um canal já aberto: envia a requisição do
/// protocolo e acumula bytes até formar um frame reconhecível ou esgotar o tempo.
/// </summary>
public static class SerialWeightReader
{
    private const int BufferChunk = 256;
    private const int MaxBufferBytes = 4096;

    public static WeightReading Read(
        ISerialChannel channel,
        IBalancaProtocol protocol,
        int timeoutMs,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(protocol);

        channel.DiscardInBuffer();

        byte[] request = protocol.BuildRequest();
        if (request.Length > 0)
            channel.Write(request);

        var accumulated = new List<byte>(BufferChunk);
        var chunk = new byte[BufferChunk];
        var sw = Stopwatch.StartNew();

        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            ct.ThrowIfCancellationRequested();

            int n = channel.Read(chunk, 0, chunk.Length);
            if (n > 0)
            {
                accumulated.AddRange(chunk.AsSpan(0, n).ToArray());
                if (accumulated.Count > MaxBufferBytes)
                    accumulated.RemoveRange(0, accumulated.Count - MaxBufferBytes);

                if (protocol.TryParse(CollectionsMarshalSpan(accumulated), out var reading))
                    return reading;
            }
            else
            {
                Thread.Sleep(10);
            }
        }

        // Última tentativa com o que foi acumulado.
        if (accumulated.Count > 0 && protocol.TryParse(CollectionsMarshalSpan(accumulated), out var final))
            return final;

        string rawAscii = accumulated.Count > 0 ? WeightFrameParser.ToAscii(CollectionsMarshalSpan(accumulated)).Trim() : "";
        string rawHex = accumulated.Count > 0 ? WeightFrameParser.ToHex(CollectionsMarshalSpan(accumulated)) : "";
        return WeightReading.NotRead(rawAscii, rawHex);
    }

    private static ReadOnlySpan<byte> CollectionsMarshalSpan(List<byte> list) =>
        System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list);
}
