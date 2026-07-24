using Ribanense.Solucoes.App.Balanca.Domain;
using Ribanense.Solucoes.App.Balanca.Serial;

namespace Ribanense.Solucoes.App.Balanca.Services;

/// <summary>
/// Motor de varredura: gera combinações de configuração serial para um modelo e
/// testa cada uma, tanto passo a passo ("um a um") quanto em lote ("todas").
/// </summary>
public sealed class ScanEngine
{
    private readonly ISerialChannelFactory _factory;

    public ScanEngine(ISerialChannelFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>
    /// Gera os candidatos de configuração, colocando o default do modelo primeiro
    /// em cada porta e evitando repetições.
    /// </summary>
    public IReadOnlyList<SerialConfig> BuildCandidates(
        BalancaModel model,
        IReadOnlyList<string> ports,
        ScanOptions options)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(ports);
        options ??= ScanOptions.Default;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<SerialConfig>();

        foreach (string port in ports)
        {
            var preferred = model.DefaultConfig(port) with { TimeoutMs = options.TimeoutMsPerAttempt };
            if (seen.Add(Signature(preferred))) result.Add(preferred);

            foreach (int baud in options.BaudRates)
            foreach (int dataBits in options.DataBits)
            foreach (var parity in options.Parities)
            foreach (var stopBits in options.StopBitsSet)
            foreach (var handshake in options.Handshakes)
            {
                var cfg = new SerialConfig(port, baud, dataBits, parity, stopBits, handshake, options.TimeoutMsPerAttempt);
                if (seen.Add(Signature(cfg))) result.Add(cfg);
            }
        }

        return result;
    }

    /// <summary>Testa uma única configuração, abrindo e fechando a porta.</summary>
    public Task<ScanResult> ProbeAsync(BalancaModel model, SerialConfig config, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(config);

        return Task.Run(() =>
        {
            ISerialChannel? channel = null;
            try
            {
                channel = _factory.Create();
                channel.Open(config);
                var reading = SerialWeightReader.Read(channel, model.Protocol, config.TimeoutMs, ct);
                bool success = reading.HasResponse;
                return new ScanResult(config, reading, success);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new ScanResult(config, WeightReading.NotRead(), false, ex.Message);
            }
            finally
            {
                channel?.Dispose();
            }
        }, ct);
    }

    /// <summary>
    /// Varredura completa ("todas as portas"): testa todos os candidatos, reporta
    /// cada tentativa via <paramref name="onAttempt"/> e devolve as combinações que
    /// obtiveram resposta, ordenadas por pontuação.
    /// </summary>
    public async Task<IReadOnlyList<ScanResult>> ScanAllAsync(
        BalancaModel model,
        IReadOnlyList<string> ports,
        ScanOptions options,
        IProgress<ScanResult>? onAttempt = null,
        CancellationToken ct = default)
    {
        var candidates = BuildCandidates(model, ports, options);
        var hits = new List<ScanResult>();

        foreach (var config in candidates)
        {
            ct.ThrowIfCancellationRequested();
            var result = await ProbeAsync(model, config, ct).ConfigureAwait(false);
            onAttempt?.Report(result);
            if (result.Reading.HasResponse) hits.Add(result);
        }

        return hits
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.Reading.Weight)
            .ToList();
    }

    private static string Signature(SerialConfig c) =>
        $"{c.Port}|{c.BaudRate}|{c.DataBits}|{(int)c.Parity}|{(int)c.StopBits}|{(int)c.Handshake}";
}
