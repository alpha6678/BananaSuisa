using System.Diagnostics;
using System.Runtime.InteropServices;
using Ribanense.Solucoes.App.Balanca.Domain;
using Ribanense.Solucoes.App.Balanca.Protocols;
using Ribanense.Solucoes.App.Balanca.Serial;

namespace Ribanense.Solucoes.App.Balanca.Services;

/// <summary>
/// Núcleo de leitura de peso sobre um canal já aberto: envia a requisição do protocolo
/// e acumula bytes, processando-os de forma incremental (sem interpretar dígitos parciais)
/// até formar um frame reconhecível, esgotar o tempo ou ser cancelado.
/// </summary>
public static class SerialWeightReader
{
    private const int BufferChunk = 256;
    private const int MaxBufferBytes = 4096;

    public static SerialReadOutcome Read(
        ISerialChannel channel,
        IBalancaProtocol protocol,
        SerialReadOptions options,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(protocol);
        ArgumentNullException.ThrowIfNull(options);

        byte[] request = protocol.BuildRequest();

        // Purga apenas em protocolos de requisição/resposta (ENQ): descartamos bytes
        // obsoletos antes de pedir uma nova leitura. Em streaming (sem requisição) o
        // buffer é preservado para não perder frames que já estavam chegando.
        if (options.PurgeBeforeRequest && request.Length > 0)
            channel.DiscardInBuffer();

        int bytesSent = 0;
        if (request.Length > 0)
        {
            channel.Write(request);
            bytesSent = request.Length;
        }

        var accumulated = new List<byte>(BufferChunk);
        var chunk = new byte[BufferChunk];
        var sw = Stopwatch.StartNew();
        long? firstByteMs = null;
        long lastByteMs = 0;
        string? lineError = null;

        WeightReading? parsed = null;
        FrameConfidence confidence = FrameConfidence.None;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            long elapsed = sw.ElapsedMilliseconds;

            if (firstByteMs is null && elapsed >= options.FirstByteTimeoutMs) break;
            if (elapsed >= options.TotalTimeoutMs) break;
            if (firstByteMs is not null && elapsed - lastByteMs >= options.InterByteTimeoutMs)
            {
                // Silêncio após receber algo: tenta um flush final antes de desistir.
                if (TryParse(protocol, accumulated, isFinal: true, out var r, out var c))
                {
                    parsed = r;
                    confidence = c;
                }
                break;
            }

            int n = channel.Read(chunk, 0, chunk.Length);
            lineError ??= channel.DrainLineError();

            if (n > 0)
            {
                firstByteMs ??= sw.ElapsedMilliseconds;
                lastByteMs = sw.ElapsedMilliseconds;
                Append(accumulated, chunk, n);

                if (TryParse(protocol, accumulated, isFinal: false, out var r, out var c))
                {
                    parsed = r;
                    confidence = c;
                    break;
                }
            }
            else
            {
                Thread.Sleep(5);
            }
        }

        // Flush final se o tempo total esgotou sem frame.
        if (parsed is null && accumulated.Count > 0)
        {
            lineError ??= channel.DrainLineError();
            if (TryParse(protocol, accumulated, isFinal: true, out var r, out var c))
            {
                parsed = r;
                confidence = c;
            }
        }

        long total = sw.ElapsedMilliseconds;
        string rawHex = accumulated.Count > 0 ? WeightFrameParser.ToHex(Span(accumulated)) : "";

        if (parsed is not null)
        {
            var okDiag = new SerialReadDiagnostics(
                bytesSent, accumulated.Count, firstByteMs, total, true, "frame reconhecido", rawHex);
            return new SerialReadOutcome(parsed, confidence, okDiag);
        }

        string rawAscii = accumulated.Count > 0 ? WeightFrameParser.ToAscii(Span(accumulated)).Trim() : "";
        string reason = BuildReason(firstByteMs, accumulated.Count, lineError);
        var diag = new SerialReadDiagnostics(
            bytesSent, accumulated.Count, firstByteMs, total, false, reason, rawHex);
        return new SerialReadOutcome(WeightReading.NotRead(rawAscii, rawHex), FrameConfidence.None, diag);
    }

    /// <summary>
    /// Processa o buffer acumulado consumindo ruído e frames inválidos até encontrar um
    /// frame válido ou precisar de mais dados. Devolve <c>true</c> quando reconhece um frame.
    /// </summary>
    private static bool TryParse(
        IBalancaProtocol protocol,
        List<byte> accumulated,
        bool isFinal,
        out WeightReading? reading,
        out FrameConfidence confidence)
    {
        reading = null;
        confidence = FrameConfidence.None;

        while (accumulated.Count > 0)
        {
            var result = protocol.Read(Span(accumulated), isFinal);

            if (result.Consumed > 0)
                accumulated.RemoveRange(0, Math.Min(result.Consumed, accumulated.Count));

            switch (result.Status)
            {
                case FrameParseStatus.FrameParsed:
                    reading = result.Reading;
                    confidence = result.Confidence;
                    return reading is not null;

                case FrameParseStatus.InvalidData:
                    // Sem avanço garantiria laço infinito: pare e aguarde mais dados.
                    if (result.Consumed <= 0) return false;
                    continue;

                case FrameParseStatus.NeedMoreData:
                default:
                    return false;
            }
        }

        return false;
    }

    private static void Append(List<byte> accumulated, byte[] chunk, int count)
    {
        accumulated.AddRange(chunk.AsSpan(0, count));
        if (accumulated.Count > MaxBufferBytes)
            accumulated.RemoveRange(0, accumulated.Count - MaxBufferBytes);
    }

    private static string BuildReason(long? firstByteMs, int bytesReceived, string? lineError)
    {
        if (lineError is not null) return $"erro de linha: {lineError}";
        if (firstByteMs is null || bytesReceived == 0) return "sem resposta (timeout do primeiro byte)";
        return "resposta sem frame reconhecível";
    }

    private static ReadOnlySpan<byte> Span(List<byte> list) => CollectionsMarshal.AsSpan(list);
}
