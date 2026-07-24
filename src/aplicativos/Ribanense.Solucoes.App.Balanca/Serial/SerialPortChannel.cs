using System.IO.Ports;
using Ribanense.Solucoes.App.Balanca.Domain;

namespace Ribanense.Solucoes.App.Balanca.Serial;

/// <summary>
/// Canal serial real sobre <see cref="SerialPort"/> (COM física ou USB-serial).
/// </summary>
public sealed class SerialPortChannel : ISerialChannel
{
    // Granularidade curta de leitura; o orçamento total é controlado pelo leitor.
    private const int ReadChunkTimeoutMs = 200;

    private SerialPort? _port;

    public bool IsOpen => _port is { IsOpen: true };

    public void Open(SerialConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        Close();

        var port = new SerialPort(config.Port, config.BaudRate, config.Parity, config.DataBits, config.StopBits)
        {
            Handshake = config.Handshake,
            ReadTimeout = ReadChunkTimeoutMs,
            WriteTimeout = Math.Max(1, config.TimeoutMs),
            DtrEnable = config.Handshake is Handshake.None or Handshake.XOnXOff,
            RtsEnable = config.Handshake is Handshake.None or Handshake.XOnXOff,
        };

        port.Open();
        _port = port;
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        if (_port is not { IsOpen: true }) throw new InvalidOperationException("Porta serial não está aberta.");
        if (data.IsEmpty) return;
        byte[] buffer = data.ToArray();
        _port.Write(buffer, 0, buffer.Length);
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        if (_port is not { IsOpen: true }) throw new InvalidOperationException("Porta serial não está aberta.");
        try
        {
            return _port.Read(buffer, offset, count);
        }
        catch (TimeoutException)
        {
            return 0;
        }
    }

    public void DiscardInBuffer()
    {
        try
        {
            if (_port is { IsOpen: true })
            {
                _port.DiscardInBuffer();
                _port.DiscardOutBuffer();
            }
        }
        catch
        {
            // Descartar buffers é best-effort.
        }
    }

    public void Close()
    {
        if (_port is null) return;
        try
        {
            if (_port.IsOpen) _port.Close();
        }
        catch
        {
            // Ignorar erros ao fechar porta.
        }
        finally
        {
            _port.Dispose();
            _port = null;
        }
    }

    public void Dispose() => Close();
}
