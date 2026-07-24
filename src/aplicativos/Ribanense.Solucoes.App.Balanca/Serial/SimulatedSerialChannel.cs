using System.Globalization;
using Ribanense.Solucoes.App.Balanca.Domain;

namespace Ribanense.Solucoes.App.Balanca.Serial;

/// <summary>
/// Canal serial simulado (balança virtual). Responde com um frame de peso válido
/// apenas quando aberto com os parâmetros seriais esperados; nas demais combinações
/// simula silêncio (timeout). Permite testar a UI e a varredura sem hardware.
/// </summary>
public sealed class SimulatedSerialChannel : ISerialChannel
{
    private readonly SerialConfig _target;
    private readonly byte[] _frame;
    private readonly object _sync = new();
    private bool _open;
    private bool _matched;
    private readonly Queue<byte> _pending = new();

    public SimulatedSerialChannel(SerialConfig target, decimal weight = 5.250m, string unit = "kg")
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _frame = BuildFrame(weight, unit);
    }

    public bool IsOpen => _open;

    public void Open(SerialConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        lock (_sync)
        {
            _open = true;
            _matched = Matches(config, _target);
            _pending.Clear();
        }
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        lock (_sync)
        {
            if (!_open) throw new InvalidOperationException("Porta simulada não está aberta.");
            if (!_matched) return;
            foreach (byte b in _frame) _pending.Enqueue(b);
        }
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        lock (_sync)
        {
            if (!_open) throw new InvalidOperationException("Porta simulada não está aberta.");
            if (_pending.Count == 0) return 0;

            int read = 0;
            while (read < count && _pending.Count > 0)
            {
                buffer[offset + read] = _pending.Dequeue();
                read++;
            }
            return read;
        }
    }

    public void DiscardInBuffer()
    {
        lock (_sync) { _pending.Clear(); }
    }

    public void Close()
    {
        lock (_sync)
        {
            _open = false;
            _matched = false;
            _pending.Clear();
        }
    }

    public void Dispose() => Close();

    private static bool Matches(SerialConfig a, SerialConfig b) =>
        string.Equals(a.Port, b.Port, StringComparison.OrdinalIgnoreCase)
        && a.BaudRate == b.BaudRate
        && a.DataBits == b.DataBits
        && a.Parity == b.Parity
        && a.StopBits == b.StopBits;

    /// <summary>Frame no formato genérico: STX + peso + unidade + ETX + CRLF.</summary>
    private static byte[] BuildFrame(decimal weight, string unit)
    {
        string body = weight.ToString("000.000", CultureInfo.InvariantCulture) + unit;
        var bytes = new List<byte> { 0x02 };
        bytes.AddRange(System.Text.Encoding.ASCII.GetBytes(body));
        bytes.Add(0x03);
        bytes.Add(0x0D);
        bytes.Add(0x0A);
        return bytes.ToArray();
    }
}
