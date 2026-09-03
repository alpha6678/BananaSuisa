using System.IO;
using System.Text;

namespace Ribanense.Solucoes.App.Farol.Mesh;

public sealed record PeerRequest(
    string Method,
    string Path,
    IReadOnlyDictionary<string, string> Headers)
{
    public string? Header(string name) =>
        Headers.TryGetValue(name, out string? value) ? value : null;
}

public sealed record PeerResponse(int StatusCode, string ReasonPhrase, string Body, string ContentType)
{
    public static PeerResponse Json(string body) => new(200, "OK", body, "application/json; charset=utf-8");

    public static PeerResponse NotFound() =>
        new(404, "Not Found", "{\"error\":\"rota desconhecida\"}", "application/json; charset=utf-8");

    public static PeerResponse Forbidden() =>
        new(403, "Forbidden", "{\"error\":\"codigo da loja nao confere\"}", "application/json; charset=utf-8");

    public static PeerResponse NoContent() =>
        new(204, "No Content", string.Empty, "application/json; charset=utf-8");
}

/// <summary>
/// Leitura e escrita de HTTP/1.1 no mínimo necessário para a malha.
/// </summary>
/// <remarks>
/// O app fala HTTP sobre <c>TcpListener</c> em vez de <c>HttpListener</c> porque
/// este último exige reserva de URL (<c>netsh http add urlacl</c>) ou processo
/// elevado para escutar fora de localhost. O Farol precisa rodar como usuário
/// comum na inicialização; um socket TCP simples não tem essa restrição.
/// </remarks>
internal static class HttpPrimitives
{
    private const int MaxRequestLineLength = 2048;
    private const int MaxHeaders = 40;

    public static async Task<PeerRequest?> ReadRequestAsync(Stream stream, CancellationToken ct)
    {
        string? requestLine = await ReadLineAsync(stream, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(requestLine)) return null;

        string[] parts = requestLine.Split(' ', 3);
        if (parts.Length < 2) return null;

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < MaxHeaders; i++)
        {
            string? line = await ReadLineAsync(stream, ct).ConfigureAwait(false);
            if (string.IsNullOrEmpty(line)) break;

            int colon = line.IndexOf(':');
            if (colon <= 0) continue;

            headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }

        return new PeerRequest(parts[0].ToUpperInvariant(), parts[1], headers);
    }

    public static async Task WriteResponseAsync(Stream stream, PeerResponse response, CancellationToken ct)
    {
        byte[] body = Encoding.UTF8.GetBytes(response.Body);

        var head = new StringBuilder()
            .Append("HTTP/1.1 ").Append(response.StatusCode).Append(' ').Append(response.ReasonPhrase).Append("\r\n")
            .Append("Content-Type: ").Append(response.ContentType).Append("\r\n")
            .Append("Content-Length: ").Append(body.Length).Append("\r\n")
            .Append("Cache-Control: no-store\r\n")
            .Append("Connection: close\r\n")
            .Append("\r\n");

        byte[] headBytes = Encoding.ASCII.GetBytes(head.ToString());
        await stream.WriteAsync(headBytes, ct).ConfigureAwait(false);
        if (body.Length > 0) await stream.WriteAsync(body, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    private static async Task<string?> ReadLineAsync(Stream stream, CancellationToken ct)
    {
        var buffer = new List<byte>(128);
        var single = new byte[1];

        while (buffer.Count < MaxRequestLineLength)
        {
            int read = await stream.ReadAsync(single.AsMemory(0, 1), ct).ConfigureAwait(false);
            if (read == 0) return buffer.Count == 0 ? null : Encoding.UTF8.GetString(buffer.ToArray());

            if (single[0] == (byte)'\n')
            {
                if (buffer.Count > 0 && buffer[^1] == (byte)'\r') buffer.RemoveAt(buffer.Count - 1);
                return Encoding.UTF8.GetString(buffer.ToArray());
            }

            buffer.Add(single[0]);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
