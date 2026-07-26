using Ribanense.Solucoes.App.Balanca.Domain;

namespace Ribanense.Solucoes.App.Balanca.Services;

/// <summary>
/// Orçamentos de tempo de uma leitura serial, separando espera do primeiro byte,
/// intervalo entre bytes e teto total. Calculados a partir do baud + margem de folga.
/// </summary>
public sealed record SerialReadOptions(
    int TotalTimeoutMs,
    int FirstByteTimeoutMs,
    int InterByteTimeoutMs,
    bool PurgeBeforeRequest = true)
{
    /// <summary>
    /// Deriva os timeouts de uma configuração. O intervalo entre bytes é estimado a
    /// partir do tempo de fio de ~24 caracteres no baud escolhido, limitado a uma faixa
    /// segura para absorver latência de driver/USB (ex.: timer FTDI).
    /// </summary>
    public static SerialReadOptions FromConfig(SerialConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        int total = Math.Max(50, config.TimeoutMs);
        int perCharMs = config.BaudRate > 0 ? (int)Math.Ceiling(10_000.0 / config.BaudRate) : 1;
        int interByte = Math.Clamp(perCharMs * 24, 40, 400);
        return new SerialReadOptions(total, total, interByte);
    }
}
