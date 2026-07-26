using System.IO.Ports;

namespace Ribanense.Solucoes.App.Balanca.Services;

/// <summary>
/// Parâmetros da varredura automática. O modo normal é guiado pelo protocolo:
/// testa o default do modelo e um pequeno conjunto de bauds/formatos plausíveis.
/// <see cref="Deep"/> amplia para o produto cartesiano completo (casos difíceis).
/// </summary>
public sealed class ScanOptions
{
    public int TimeoutMsPerAttempt { get; init; } = 1500;

    public bool Deep { get; init; }

    /// <summary>Parar a varredura ao encontrar um frame de alta confiança e estável.</summary>
    public bool StopOnConfidentHit { get; init; } = true;

    public IReadOnlyList<int> BaudRates => Deep
        ? new[] { 9600, 4800, 19200, 2400, 38400, 57600, 115200, 1200, 600, 300, 110 }
        : new[] { 9600, 4800, 2400, 19200 };

    /// <summary>Formatos (data/paridade/stop) comuns testados no modo normal.</summary>
    public IReadOnlyList<(int DataBits, Parity Parity, StopBits StopBits)> FramingProfiles { get; } =
        new[]
        {
            (8, Parity.None, StopBits.One),
            (8, Parity.None, StopBits.Two),
            (8, Parity.Even, StopBits.One),
            (7, Parity.Even, StopBits.One),
        };

    // Conjuntos completos usados apenas no modo profundo.
    public IReadOnlyList<int> DataBits => new[] { 8, 7, 6, 5 };
    public IReadOnlyList<Parity> Parities =>
        new[] { Parity.None, Parity.Even, Parity.Odd, Parity.Mark, Parity.Space };
    public IReadOnlyList<StopBits> StopBitsSet =>
        new[] { StopBits.One, StopBits.Two, StopBits.OnePointFive };
    public IReadOnlyList<Handshake> Handshakes =>
        new[] { Handshake.None, Handshake.RequestToSend, Handshake.XOnXOff };

    public static ScanOptions Default => new();
}
