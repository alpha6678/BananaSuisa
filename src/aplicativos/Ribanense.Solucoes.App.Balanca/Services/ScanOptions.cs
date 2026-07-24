using System.IO.Ports;

namespace Ribanense.Solucoes.App.Balanca.Services;

/// <summary>
/// Conjuntos de parâmetros seriais a combinar durante a varredura automática.
/// O padrão cobre as configurações mais comuns; <see cref="Deep"/> amplia a
/// matriz (mais bauds, data bits e stop bits) para casos difíceis.
/// </summary>
public sealed class ScanOptions
{
    public int TimeoutMsPerAttempt { get; init; } = 1500;

    public bool Deep { get; init; }

    public IReadOnlyList<int> BaudRates => Deep
        ? new[] { 9600, 4800, 19200, 2400, 38400, 57600, 115200, 1200, 600, 300, 110 }
        : new[] { 9600, 4800, 19200, 2400, 38400 };

    public IReadOnlyList<int> DataBits => Deep
        ? new[] { 8, 7, 6, 5 }
        : new[] { 8, 7 };

    public IReadOnlyList<Parity> Parities => Deep
        ? new[] { Parity.None, Parity.Even, Parity.Odd, Parity.Mark, Parity.Space }
        : new[] { Parity.None, Parity.Even, Parity.Odd };

    public IReadOnlyList<StopBits> StopBitsSet => Deep
        ? new[] { StopBits.One, StopBits.Two, StopBits.OnePointFive }
        : new[] { StopBits.One };

    public IReadOnlyList<Handshake> Handshakes => Deep
        ? new[] { Handshake.None, Handshake.RequestToSend, Handshake.XOnXOff }
        : new[] { Handshake.None };

    public static ScanOptions Default => new();
}
