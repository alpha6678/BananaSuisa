using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Ribanense.Solucoes.App.Scripts.Scripts.Dns;

/// <summary>
/// Implementação real de <see cref="IDnsQueryProbe"/>: monta uma consulta DNS
/// (registro A) crua conforme a RFC 1035 e a envia via UDP diretamente ao
/// servidor informado, na porta 53, cronometrando a resposta. Isso garante
/// que a latência medida é a do servidor testado, e não a de um cache local.
/// </summary>
public sealed class UdpDnsQueryProbe : IDnsQueryProbe
{
    private const int DnsPort = 53;

    public async Task<DnsQueryAttempt> QueryAsync(string serverIp, string domain, TimeSpan timeout, CancellationToken ct)
    {
        if (!IPAddress.TryParse(serverIp, out var address))
        {
            return new DnsQueryAttempt(false, 0, "endereço IP inválido.");
        }

        byte[] query;
        try
        {
            query = BuildQuery(domain, out ushort _);
        }
        catch (Exception ex)
        {
            return new DnsQueryAttempt(false, 0, $"domínio inválido: {ex.Message}");
        }

        ushort transactionId = (ushort)((query[0] << 8) | query[1]);
        var sw = Stopwatch.StartNew();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linkedCts.CancelAfter(timeout);

        try
        {
            using var udp = new UdpClient(address.AddressFamily);
            await udp.SendAsync(query, new IPEndPoint(address, DnsPort), linkedCts.Token).ConfigureAwait(false);

            UdpReceiveResult response = await udp.ReceiveAsync(linkedCts.Token).ConfigureAwait(false);
            sw.Stop();

            if (response.Buffer.Length < 4)
            {
                return new DnsQueryAttempt(false, sw.Elapsed.TotalMilliseconds, "resposta inválida (muito curta).");
            }

            ushort responseId = (ushort)((response.Buffer[0] << 8) | response.Buffer[1]);
            if (responseId != transactionId)
            {
                return new DnsQueryAttempt(false, sw.Elapsed.TotalMilliseconds, "ID de transação não corresponde.");
            }

            int rcode = response.Buffer[3] & 0x0F;
            if (rcode != 0)
            {
                return new DnsQueryAttempt(false, sw.Elapsed.TotalMilliseconds, $"servidor retornou rcode {rcode}.");
            }

            return new DnsQueryAttempt(true, sw.Elapsed.TotalMilliseconds, null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new DnsQueryAttempt(false, timeout.TotalMilliseconds, "tempo esgotado.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new DnsQueryAttempt(false, sw.Elapsed.TotalMilliseconds, ex.Message);
        }
    }

    private static byte[] BuildQuery(string domain, out ushort transactionId)
    {
        if (string.IsNullOrWhiteSpace(domain)) throw new ArgumentException("domínio vazio.");

        transactionId = (ushort)Random.Shared.Next(0, ushort.MaxValue + 1);

        using var ms = new MemoryStream();
        WriteUInt16(ms, transactionId);
        WriteUInt16(ms, 0x0100); // flags: consulta padrão, recursão desejada
        WriteUInt16(ms, 1);      // QDCOUNT
        WriteUInt16(ms, 0);      // ANCOUNT
        WriteUInt16(ms, 0);      // NSCOUNT
        WriteUInt16(ms, 0);      // ARCOUNT

        foreach (var label in domain.Trim().TrimEnd('.').Split('.'))
        {
            if (label.Length == 0) continue;
            byte[] labelBytes = Encoding.ASCII.GetBytes(label);
            ms.WriteByte((byte)labelBytes.Length);
            ms.Write(labelBytes, 0, labelBytes.Length);
        }
        ms.WriteByte(0); // fim do QNAME

        WriteUInt16(ms, 1); // QTYPE = A
        WriteUInt16(ms, 1); // QCLASS = IN

        return ms.ToArray();
    }

    private static void WriteUInt16(Stream stream, int value)
    {
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }
}
