using System.IO.Ports;
using Ribanense.Solucoes.App.Balanca.Domain;
using Ribanense.Solucoes.App.Balanca.Protocols;
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
    /// Gera os candidatos de configuração guiados pelo protocolo: primeiro o default
    /// do modelo, depois os bauds documentados com os formatos plausíveis. No modo
    /// profundo, expande para o produto cartesiano completo. O timeout é adaptado ao
    /// baud (bauds baixos ganham mais tempo).
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
            // 1) Default documentado do modelo tem prioridade absoluta.
            var preferred = model.DefaultConfig(port) with { TimeoutMs = TimeoutFor(model.DefaultConfig(port).BaudRate, options) };
            if (seen.Add(Signature(preferred))) result.Add(preferred);

            if (options.Deep)
            {
                foreach (int baud in options.BaudRates)
                foreach (int dataBits in options.DataBits)
                foreach (var parity in options.Parities)
                foreach (var stopBits in options.StopBitsSet)
                foreach (var handshake in options.Handshakes)
                {
                    Add(new SerialConfig(port, baud, dataBits, parity, stopBits, handshake, TimeoutFor(baud, options)));
                }
            }
            else
            {
                // 2) Bauds documentados × formatos plausíveis, sem handshake.
                foreach (int baud in options.BaudRates)
                foreach (var (dataBits, parity, stopBits) in options.FramingProfiles)
                {
                    Add(new SerialConfig(port, baud, dataBits, parity, stopBits, Handshake.None, TimeoutFor(baud, options)));
                }
            }
        }

        return result;

        void Add(SerialConfig cfg)
        {
            if (seen.Add(Signature(cfg))) result.Add(cfg);
        }
    }

    /// <summary>Timeout por tentativa adaptado ao baud: bauds baixos precisam de mais tempo.</summary>
    private static int TimeoutFor(int baud, ScanOptions options)
    {
        int baseTimeout = options.TimeoutMsPerAttempt;
        if (baud is <= 0 or >= 2400) return baseTimeout;
        // Um frame curto a 300 baud já custa centenas de ms; dobra o orçamento abaixo de 2400.
        return baseTimeout * 2;
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
                var options = SerialReadOptions.FromConfig(config);
                var outcome = SerialWeightReader.Read(channel, model.Protocol, options, ct);
                bool success = outcome.Reading.HasResponse;
                string? error = success ? null : outcome.Diagnostics.Reason;
                return new ScanResult(config, outcome.Reading, success, outcome.Confidence, error);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (SerialChannelException ex)
            {
                return new ScanResult(config, WeightReading.NotRead(), false, FrameConfidence.None, $"{ex.FaultLabel}: {ex.Message}");
            }
            catch (Exception ex)
            {
                return new ScanResult(config, WeightReading.NotRead(), false, FrameConfidence.None, ex.Message);
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
        options ??= ScanOptions.Default;
        var candidates = BuildCandidates(model, ports, options);
        var hits = new List<ScanResult>();

        foreach (var config in candidates)
        {
            ct.ThrowIfCancellationRequested();
            var result = await ProbeAsync(model, config, ct).ConfigureAwait(false);
            onAttempt?.Report(result);
            if (result.Reading.HasResponse) hits.Add(result);

            // Parada antecipada: frame de alta confiança e leitura estável já resolve.
            if (options.StopOnConfidentHit
                && result.Confidence == Protocols.FrameConfidence.High
                && result.Reading.Status == Domain.WeightStatus.Estavel)
            {
                break;
            }
        }

        return hits
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.Reading.Weight)
            .ToList();
    }

    private static string Signature(SerialConfig c) =>
        $"{c.Port}|{c.BaudRate}|{c.DataBits}|{(int)c.Parity}|{(int)c.StopBits}|{(int)c.Handshake}";
}
