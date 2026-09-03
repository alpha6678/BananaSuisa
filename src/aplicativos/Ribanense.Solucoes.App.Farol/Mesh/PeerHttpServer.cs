using System.IO;
using System.Net;
using System.Net.Sockets;
using Ribanense.Solucoes.App.Farol.Domain;

namespace Ribanense.Solucoes.App.Farol.Mesh;

/// <summary>Fonte dos dados que este farol publica para os pares.</summary>
public interface IPeerDataSource
{
    HealthSignal GetHealth();
    EvidenceBundle? GetLatestBundle();
    EvidenceBundle? GetBundle(Guid id);
}

/// <summary>
/// API entre pares: <c>/health</c>, <c>/bundle/latest</c> e <c>/bundle/{id}</c>.
/// Toda requisição precisa trazer o cabeçalho <c>X-Farol-Store</c> com o hash do
/// código da loja; sem isso responde 403 e nada vaza.
/// </summary>
public sealed class PeerHttpServer : IDisposable
{
    public const string StoreHeader = "X-Farol-Store";

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    private readonly int _port;
    private readonly PairingStore _pairing;
    private readonly IPeerDataSource _data;
    private readonly Action<string, Exception?>? _log;

    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _acceptLoop;
    private bool _disposed;

    public PeerHttpServer(
        PairingStore pairing,
        IPeerDataSource data,
        int port,
        Action<string, Exception?>? log = null)
    {
        _pairing = pairing ?? throw new ArgumentNullException(nameof(pairing));
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _port = port;
        _log = log;
    }

    public bool IsRunning => _listener is not null;
    public string? StartupError { get; private set; }

    public void Start()
    {
        if (_listener is not null || _disposed) return;

        try
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            StartupError = null;
        }
        catch (SocketException ex)
        {
            StartupError = $"Porta TCP {_port} indisponível: {ex.SocketErrorCode}.";
            _log?.Invoke(StartupError, ex);
            _listener = null;
            return;
        }

        _cts = new CancellationTokenSource();
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        TcpListener? listener = _listener;
        if (listener is null) return;

        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                continue;
            }

            // Cada conexão é curta e independente: atender em paralelo evita que
            // um par lento segure a fila dos demais.
            _ = Task.Run(() => ServeAsync(client, ct), CancellationToken.None);
        }
    }

    private async Task ServeAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(RequestTimeout);

            try
            {
                await using NetworkStream stream = client.GetStream();

                PeerRequest? request = await HttpPrimitives.ReadRequestAsync(stream, timeout.Token).ConfigureAwait(false);
                if (request is null) return;

                PeerResponse response = Handle(request);
                await HttpPrimitives.WriteResponseAsync(stream, response, timeout.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or SocketException)
            {
                // Par desistiu ou a conexão caiu no meio: nada a registrar.
            }
            catch (Exception ex)
            {
                _log?.Invoke("Falha ao atender requisição de par.", ex);
            }
        }
    }

    internal PeerResponse Handle(PeerRequest request)
    {
        if (request.Method != "GET") return PeerResponse.NotFound();
        if (!_pairing.Accepts(request.Header(StoreHeader))) return PeerResponse.Forbidden();

        string path = request.Path.Split('?')[0].TrimEnd('/');

        if (path is "" or "/health")
        {
            return PeerResponse.Json(FarolJson.Serialize(_data.GetHealth(), indented: false));
        }

        if (path == "/bundle/latest")
        {
            EvidenceBundle? latest = _data.GetLatestBundle();
            return latest is null
                ? PeerResponse.NoContent()
                : PeerResponse.Json(FarolJson.Serialize(latest, indented: false));
        }

        if (path.StartsWith("/bundle/", StringComparison.Ordinal))
        {
            string raw = path["/bundle/".Length..];
            if (!Guid.TryParse(raw, out Guid id)) return PeerResponse.NotFound();

            EvidenceBundle? bundle = _data.GetBundle(id);
            return bundle is null
                ? PeerResponse.NoContent()
                : PeerResponse.Json(FarolJson.Serialize(bundle, indented: false));
        }

        return PeerResponse.NotFound();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try { _cts?.Cancel(); } catch { }
        try { _listener?.Stop(); } catch { }
        _listener = null;

        try { _acceptLoop?.Wait(TimeSpan.FromSeconds(2)); } catch { }

        _cts?.Dispose();
        _cts = null;
    }
}
