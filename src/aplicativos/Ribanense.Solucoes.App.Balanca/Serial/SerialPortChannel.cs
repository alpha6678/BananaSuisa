using System.IO.Ports;
using Ribanense.Solucoes.App.Balanca.Domain;

namespace Ribanense.Solucoes.App.Balanca.Serial;

/// <summary>
/// Canal serial real sobre <see cref="SerialPort"/> (COM física ou USB-serial).
/// Mapeia falhas para <see cref="SerialChannelException"/> e captura erros de linha.
/// </summary>
public sealed class SerialPortChannel : ISerialChannel
{
    // Granularidade curta de leitura; o orçamento total é controlado pelo leitor.
    // Mantida baixa para que cancelamento e detecção de intervalo entre bytes sejam ágeis.
    private const int ReadChunkTimeoutMs = 60;

    private SerialPort? _port;
    private volatile string? _lineError;

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
            // DTR/RTS explícitos: ligados quando não há controle de fluxo por hardware,
            // pois muitas balanças só transmitem com essas linhas ativas.
            DtrEnable = config.Handshake is Handshake.None or Handshake.XOnXOff,
            RtsEnable = config.Handshake is Handshake.None or Handshake.XOnXOff,
        };
        port.ErrorReceived += OnErrorReceived;

        try
        {
            port.Open();
        }
        catch (UnauthorizedAccessException ex)
        {
            port.Dispose();
            throw new SerialChannelException(SerialFault.Busy,
                $"Porta {config.Port} ocupada ou sem permissão de acesso.", ex);
        }
        catch (ArgumentException ex)
        {
            port.Dispose();
            throw new SerialChannelException(SerialFault.NotFound,
                $"Porta {config.Port} inexistente ou configuração inválida.", ex);
        }
        catch (System.IO.FileNotFoundException ex)
        {
            port.Dispose();
            throw new SerialChannelException(SerialFault.NotFound,
                $"Porta {config.Port} não encontrada.", ex);
        }
        catch (System.IO.IOException ex)
        {
            port.Dispose();
            throw new SerialChannelException(SerialFault.Unknown,
                $"Falha de E/S ao abrir {config.Port}: {ex.Message}", ex);
        }

        _lineError = null;
        _port = port;
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        if (_port is not { IsOpen: true }) throw new InvalidOperationException("Porta serial não está aberta.");
        if (data.IsEmpty) return;
        byte[] buffer = data.ToArray();
        try
        {
            _port.Write(buffer, 0, buffer.Length);
        }
        catch (System.IO.IOException ex)
        {
            throw new SerialChannelException(SerialFault.Disconnected,
                "Falha ao escrever na porta (dispositivo removido?).", ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new SerialChannelException(SerialFault.Disconnected, "Porta fechada durante a escrita.", ex);
        }
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
        catch (System.IO.IOException ex)
        {
            throw new SerialChannelException(SerialFault.Disconnected,
                "Leitura interrompida (dispositivo removido?).", ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new SerialChannelException(SerialFault.Disconnected, "Porta fechada durante a leitura.", ex);
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

    public string? DrainLineError()
    {
        string? current = _lineError;
        _lineError = null;
        return current;
    }

    public void Close()
    {
        if (_port is null) return;
        try
        {
            _port.ErrorReceived -= OnErrorReceived;
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

    private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e) =>
        _lineError = e.EventType switch
        {
            SerialError.Frame => "framing",
            SerialError.RXParity => "paridade",
            SerialError.Overrun => "overrun",
            SerialError.RXOver => "buffer cheio (RX)",
            SerialError.TXFull => "buffer cheio (TX)",
            _ => e.EventType.ToString(),
        };
}
