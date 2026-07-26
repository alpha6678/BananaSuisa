using System.Runtime.InteropServices;
using Ribanense.Solucoes.App.Balanca.Domain;
using Ribanense.Solucoes.App.Balanca.Protocols;
using Ribanense.Solucoes.App.Balanca.Serial;

namespace Ribanense.Solucoes.App.Balanca.Tests;

/// <summary>Alimenta um protocolo de forma incremental, replicando a lógica de consumo do leitor.</summary>
internal static class IncrementalFeeder
{
    /// <summary>
    /// Entrega <paramref name="data"/> em pedaços de <paramref name="chunkSize"/> bytes,
    /// consumindo ruído/frames inválidos, e devolve a primeira leitura reconhecida (ou null).
    /// </summary>
    public static WeightReading? Feed(IBalancaProtocol protocol, byte[] data, int chunkSize)
    {
        var acc = new List<byte>();
        int i = 0;
        while (i < data.Length)
        {
            int take = Math.Min(chunkSize, data.Length - i);
            acc.AddRange(data.AsSpan(i, take).ToArray());
            i += take;

            if (TryConsume(protocol, acc, isFinal: false, out var reading))
                return reading;
        }

        // Flush final (timeout).
        return TryConsume(protocol, acc, isFinal: true, out var final) ? final : null;
    }

    private static bool TryConsume(IBalancaProtocol protocol, List<byte> acc, bool isFinal, out WeightReading? reading)
    {
        reading = null;
        while (acc.Count > 0)
        {
            var result = protocol.Read(CollectionsMarshal.AsSpan(acc), isFinal);
            if (result.Consumed > 0)
                acc.RemoveRange(0, Math.Min(result.Consumed, acc.Count));

            switch (result.Status)
            {
                case FrameParseStatus.FrameParsed:
                    reading = result.Reading;
                    return reading is not null;
                case FrameParseStatus.InvalidData:
                    if (result.Consumed <= 0) return false;
                    continue;
                default:
                    return false;
            }
        }
        return false;
    }
}

/// <summary>
/// Canal serial roteirizável: entrega respostas em pedaços, simula latência (chunks vazios),
/// erros de linha, purga (stale buffer) e desconexão.
/// </summary>
internal sealed class ScriptedSerialChannel : ISerialChannel
{
    private readonly Queue<byte[]> _script;
    private readonly Queue<byte> _ready = new();
    private readonly bool _respondOnEnq;
    private byte[]? _initialStale;
    private bool _open;
    private int _reads;

    public ScriptedSerialChannel(IEnumerable<byte[]> responseChunks, bool respondOnEnq = true)
    {
        _script = new Queue<byte[]>(responseChunks);
        _respondOnEnq = respondOnEnq;
    }

    /// <summary>Bytes obsoletos presentes no buffer ao abrir (para testar purga).</summary>
    public byte[]? InitialStale { init => _initialStale = value; }

    /// <summary>Erro de linha a ser drenado uma vez.</summary>
    public string? LineErrorOnce { get; set; }

    /// <summary>Lança desconexão após esta quantidade de leituras (null = nunca).</summary>
    public int? ThrowDisconnectAfterReads { get; set; }

    public bool IsOpen => _open;

    public void Open(SerialConfig config)
    {
        _open = true;
        _ready.Clear();
        if (_initialStale is { Length: > 0 })
            foreach (var b in _initialStale) _ready.Enqueue(b);

        if (!_respondOnEnq)
            DrainScriptIntoReady();
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        if (_respondOnEnq && data.IndexOf(SerialControl.ENQ) >= 0)
            DrainScriptIntoReady();
    }

    private void DrainScriptIntoReady()
    {
        while (_script.Count > 0)
            foreach (var b in _script.Dequeue())
                _ready.Enqueue(b);
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        _reads++;
        if (ThrowDisconnectAfterReads is { } n && _reads > n)
            throw new SerialChannelException(SerialFault.Disconnected, "desconectada (teste).");

        if (_ready.Count == 0) return 0;
        int read = 0;
        while (read < count && _ready.Count > 0)
            buffer[offset + read++] = _ready.Dequeue();
        return read;
    }

    public void DiscardInBuffer() => _ready.Clear();

    public string? DrainLineError()
    {
        var e = LineErrorOnce;
        LineErrorOnce = null;
        return e;
    }

    public void Close() => _open = false;

    public void Dispose() => Close();
}

/// <summary>Fábrica de canais que devolve instâncias criadas por um delegate.</summary>
internal sealed class FakeChannelFactory : ISerialChannelFactory
{
    private readonly Func<ISerialChannel> _create;
    private readonly string _port;

    public FakeChannelFactory(Func<ISerialChannel> create, string port = "COM-TEST")
    {
        _create = create;
        _port = port;
    }

    public ISerialChannel Create() => _create();

    public IReadOnlyList<SerialPortInfo> ListPorts() => new[] { new SerialPortInfo(_port, "Fake") };
}
