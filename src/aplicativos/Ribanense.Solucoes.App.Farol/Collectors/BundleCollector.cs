using System.Diagnostics;
using Ribanense.Solucoes.App.Farol.Domain;

namespace Ribanense.Solucoes.App.Farol.Collectors;

/// <summary>
/// Roda todos os coletores em sequência e monta o dossiê. Isola cada sensor:
/// uma exceção vira um <see cref="CollectorOutcome"/> e a captura continua.
/// </summary>
public sealed class BundleCollector
{
    private readonly IReadOnlyList<ICollector> _collectors;
    private readonly TimeSpan _perCollectorTimeout;

    public BundleCollector(IReadOnlyList<ICollector> collectors, TimeSpan? perCollectorTimeout = null)
    {
        _collectors = collectors ?? throw new ArgumentNullException(nameof(collectors));
        _perCollectorTimeout = perCollectorTimeout ?? TimeSpan.FromSeconds(20);
    }

    public async Task<EvidenceBundle> CaptureAsync(
        FarolIdentitySnapshot identity,
        CancellationToken ct)
    {
        var builder = new EvidenceBundleBuilder();
        var outcomes = new List<CollectorOutcome>(_collectors.Count);

        foreach (ICollector collector in _collectors)
        {
            ct.ThrowIfCancellationRequested();
            outcomes.Add(await RunOneAsync(collector, builder, ct).ConfigureAwait(false));
        }

        return new EvidenceBundle
        {
            MachineId = identity.MachineId,
            MachineName = identity.MachineName,
            FriendlyName = identity.FriendlyName,
            FarolVersion = identity.Version,
            Collectors = outcomes,
            Identity = builder.Identity,
            Network = builder.Network,
            Disks = builder.Disks.ToArray(),
            Services = builder.Services.ToArray(),
            Printers = builder.Printers.ToArray(),
            Events = builder.Events.ToArray(),
            RibanenseApps = builder.RibanenseApps.ToArray(),
            TopProcesses = builder.TopProcesses.ToArray(),
        };
    }

    private async Task<CollectorOutcome> RunOneAsync(
        ICollector collector,
        EvidenceBundleBuilder builder,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_perCollectorTimeout);

        try
        {
            await collector.CollectAsync(builder, timeout.Token).ConfigureAwait(false);
            return Outcome(collector, CollectorStatus.Ok, null, stopwatch);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Outcome(collector, CollectorStatus.Failed, "Tempo esgotado.", stopwatch);
        }
        catch (CollectorDeniedException denied)
        {
            return Outcome(collector, CollectorStatus.Denied, denied.Message, stopwatch);
        }
        catch (UnauthorizedAccessException denied)
        {
            return Outcome(collector, CollectorStatus.Denied, denied.Message, stopwatch);
        }
        catch (PlatformNotSupportedException skipped)
        {
            return Outcome(collector, CollectorStatus.Skipped, skipped.Message, stopwatch);
        }
        catch (Exception ex)
        {
            return Outcome(collector, CollectorStatus.Failed, ex.Message, stopwatch);
        }
    }

    private static CollectorOutcome Outcome(
        ICollector collector,
        CollectorStatus status,
        string? detail,
        Stopwatch stopwatch) =>
        new(collector.Id, collector.DisplayName, status, detail, (int)stopwatch.ElapsedMilliseconds);
}

/// <summary>Identidade desta máquina, injetada na captura.</summary>
public sealed record FarolIdentitySnapshot(
    string MachineId,
    string MachineName,
    string FriendlyName,
    string Version);
