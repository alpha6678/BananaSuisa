using Ribanense.Solucoes.App.Farol.Analysis;
using Ribanense.Solucoes.App.Farol.Collectors;
using Ribanense.Solucoes.App.Farol.Configuration;
using Ribanense.Solucoes.App.Farol.Domain;
using Ribanense.Solucoes.App.Farol.Mesh;
using Ribanense.Solucoes.PluginSDK.Logging;

namespace Ribanense.Solucoes.App.Farol.Services;

public sealed record CaptureResult(EvidenceBundle Bundle, IReadOnlyList<Finding> Findings);

/// <summary>
/// O farol desta máquina: coleta, avalia, publica para os pares e acompanha os
/// vizinhos. Concentrar isso aqui mantém as ViewModels livres de rede e WMI.
/// </summary>
public sealed class FarolStation : IPeerDataSource, IDisposable
{
    private static readonly TimeSpan HealthPollInterval = TimeSpan.FromSeconds(45);

    private readonly PairingStore _pairing;
    private readonly BundleStore _store;
    private readonly BundleCollector _collector;
    private readonly FindingEngine _engine;
    private readonly PeerRegistry _registry;
    private readonly PeerClient _client;
    private readonly DiscoveryResponder _discovery;
    private readonly PeerHttpServer _server;
    private readonly IAppJsonLog _log;
    private readonly string _version;

    private readonly SemaphoreSlim _captureGate = new(1, 1);
    private CancellationTokenSource? _pollCts;
    private Task? _pollLoop;
    private bool _disposed;

    public FarolStation(
        PairingStore pairing,
        BundleStore store,
        BundleCollector collector,
        FindingEngine engine,
        IAppJsonLog log,
        string version)
    {
        _pairing = pairing ?? throw new ArgumentNullException(nameof(pairing));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _collector = collector ?? throw new ArgumentNullException(nameof(collector));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _version = version;

        _registry = new PeerRegistry(_pairing.MachineId);
        _client = new PeerClient(_pairing);
        _discovery = new DiscoveryResponder(
            _registry, _pairing, BuildHello, FarolAppConfig.DiscoveryPort, LogMesh);
        _server = new PeerHttpServer(
            _pairing, this, FarolAppConfig.PeerPort, LogMesh);

        LatestBundle = _store.GetLatest();
        LatestFindings = LatestBundle is null
            ? Array.Empty<Finding>()
            : _engine.Evaluate(LatestBundle, _registry.Snapshot());
    }

    public PairingStore Pairing => _pairing;
    public PeerRegistry Peers => _registry;
    public PeerClient Client => _client;
    public BundleStore Store => _store;

    public EvidenceBundle? LatestBundle { get; private set; }
    public IReadOnlyList<Finding> LatestFindings { get; private set; } = Array.Empty<Finding>();

    public bool MeshRunning => _discovery.IsRunning && _server.IsRunning;
    public string? MeshError => _server.StartupError;

    public event Action<CaptureResult>? Captured;

    public void StartMesh()
    {
        if (_disposed || !_pairing.IsPaired || !_pairing.MeshEnabled) return;

        _server.Start();
        _discovery.Start();

        if (_pollCts is not null) return;

        _pollCts = new CancellationTokenSource();
        _pollLoop = Task.Run(() => PollPeersAsync(_pollCts.Token));

        _log.Write(AppLogLevel.Information, "mesh",
            $"Malha ativa (UDP {FarolAppConfig.DiscoveryPort}, TCP {FarolAppConfig.PeerPort}).");
    }

    public Task AnnounceAsync(CancellationToken ct = default) => _discovery.AnnounceNowAsync(ct);

    public async Task<CaptureResult> CaptureAsync(CancellationToken ct)
    {
        await _captureGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var identity = new FarolIdentitySnapshot(
                _pairing.MachineId,
                Environment.MachineName,
                _pairing.FriendlyName,
                _version);

            EvidenceBundle bundle = await _collector.CaptureAsync(identity, ct).ConfigureAwait(false);
            IReadOnlyList<Finding> findings = _engine.Evaluate(bundle, _registry.Snapshot());

            _store.Save(bundle);
            LatestBundle = bundle;
            LatestFindings = findings;

            _log.Write(AppLogLevel.Information, "capture",
                $"Dossiê capturado com {findings.Count} achado(s).");

            var result = new CaptureResult(bundle, findings);
            Captured?.Invoke(result);
            return result;
        }
        finally
        {
            _captureGate.Release();
        }
    }

    public HealthSignal GetHealth()
    {
        IReadOnlyList<Finding> findings = LatestFindings;

        return new HealthSignal
        {
            MachineId = _pairing.MachineId,
            MachineName = Environment.MachineName,
            FriendlyName = _pairing.FriendlyName,
            Version = _version,
            Level = LatestBundle is null ? HealthLevel.Desconhecido : HealthSignal.LevelFor(findings),
            HighFindings = findings.Count(f => f.Severity == FindingSeverity.High),
            MediumFindings = findings.Count(f => f.Severity == FindingSeverity.Medium),
            Headline = LatestBundle is null
                ? "Nenhuma captura ainda."
                : RuleBasedExplainer.Headline(findings),
            LastCaptureAt = LatestBundle?.CapturedAt,
            LastBundleId = LatestBundle?.Id,
        };
    }

    public EvidenceBundle? GetLatestBundle() => LatestBundle;

    public EvidenceBundle? GetBundle(Guid id) =>
        LatestBundle?.Id == id ? LatestBundle : _store.GetById(id);

    private FarolHello BuildHello() => new()
    {
        MachineId = _pairing.MachineId,
        MachineName = Environment.MachineName,
        FriendlyName = _pairing.FriendlyName,
        StoreCodeHash = _pairing.StoreCodeHash ?? string.Empty,
        Version = _version,
        PeerPort = FarolAppConfig.PeerPort,
    };

    /// <summary>
    /// Puxa o sinal leve de cada par conhecido. Só <c>/health</c>: baixar dossiê
    /// inteiro em ciclo automático encheria a rede da loja sem necessidade.
    /// </summary>
    private async Task PollPeersAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(HealthPollInterval);

        try
        {
            do
            {
                foreach (PeerBeacon peer in _registry.Snapshot())
                {
                    ct.ThrowIfCancellationRequested();

                    HealthSignal? health = await _client.GetHealthAsync(peer, ct).ConfigureAwait(false);
                    if (health is not null) _registry.AttachHealth(peer.MachineId, health);
                }
            }
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void LogMesh(string message, Exception? ex) =>
        _log.Write(ex is null ? AppLogLevel.Information : AppLogLevel.Warning, "mesh", message, ex);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try { _pollCts?.Cancel(); } catch { }
        try { _pollLoop?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _pollCts?.Dispose();

        _discovery.Dispose();
        _server.Dispose();
        _client.Dispose();
        _captureGate.Dispose();
    }
}
