using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using Ribanense.Solucoes.App.Farol.Domain;

namespace Ribanense.Solucoes.App.Farol.Mesh;

/// <summary>
/// Descoberta na LAN por broadcast UDP: anuncia este farol periodicamente e
/// escuta os anúncios dos vizinhos.
/// </summary>
/// <remarks>
/// Broadcast dirigido por adaptador em vez de 255.255.255.255 porque máquinas
/// com várias interfaces (Wi-Fi + cabo + adaptadores virtuais de VPN) só
/// entregam o pacote na sub-rede correta dessa forma. O socket de escuta usa
/// <c>ReuseAddress</c> para conviver com outra instância na mesma porta sem
/// derrubar nenhuma das duas.
/// </remarks>
public sealed class DiscoveryResponder : IDisposable
{
    public static readonly TimeSpan AnnounceInterval = TimeSpan.FromSeconds(20);

    private readonly int _port;
    private readonly PeerRegistry _registry;
    private readonly PairingStore _pairing;
    private readonly Func<FarolHello> _helloFactory;
    private readonly Action<string, Exception?>? _log;

    private UdpClient? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenLoop;
    private Task? _announceLoop;
    private bool _disposed;

    public DiscoveryResponder(
        PeerRegistry registry,
        PairingStore pairing,
        Func<FarolHello> helloFactory,
        int port,
        Action<string, Exception?>? log = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _pairing = pairing ?? throw new ArgumentNullException(nameof(pairing));
        _helloFactory = helloFactory ?? throw new ArgumentNullException(nameof(helloFactory));
        _port = port;
        _log = log;
    }

    public bool IsRunning => _listener is not null;

    public void Start()
    {
        if (_listener is not null || _disposed) return;

        _cts = new CancellationTokenSource();

        try
        {
            _listener = new UdpClient(AddressFamily.InterNetwork);
            _listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listener.Client.Bind(new IPEndPoint(IPAddress.Any, _port));
            _listener.EnableBroadcast = true;
        }
        catch (SocketException ex)
        {
            _log?.Invoke($"Descoberta indisponível na porta UDP {_port}.", ex);
            _listener?.Dispose();
            _listener = null;
            return;
        }

        _listenLoop = Task.Run(() => ListenAsync(_cts.Token));
        _announceLoop = Task.Run(() => AnnounceLoopAsync(_cts.Token));
    }

    /// <summary>Anúncio imediato, usado ao abrir a janela para não esperar o ciclo.</summary>
    public async Task AnnounceNowAsync(CancellationToken ct = default)
    {
        if (!_pairing.IsPaired) return;
        await AnnounceAsync(ct).ConfigureAwait(false);
    }

    private async Task AnnounceLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(AnnounceInterval);

        try
        {
            do
            {
                if (_pairing.IsPaired && _pairing.MeshEnabled)
                {
                    await AnnounceAsync(ct).ConfigureAwait(false);
                }
            }
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task AnnounceAsync(CancellationToken ct)
    {
        byte[] payload = Encoding.UTF8.GetBytes(FarolJson.Serialize(_helloFactory(), indented: false));

        foreach (IPAddress broadcast in BroadcastAddresses())
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var sender = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
                await sender.SendAsync(payload, new IPEndPoint(broadcast, _port), ct).ConfigureAwait(false);
            }
            catch (SocketException)
            {
                // Adaptador que sumiu ou rede sem rota: os demais seguem.
            }
        }
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        UdpClient? listener = _listener;
        if (listener is null) return;

        while (!ct.IsCancellationRequested)
        {
            UdpReceiveResult received;
            try
            {
                received = await listener.ReceiveAsync(ct).ConfigureAwait(false);
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

            HandleDatagram(received);
        }
    }

    private void HandleDatagram(UdpReceiveResult received)
    {
        FarolHello? hello;
        try
        {
            hello = FarolJson.Deserialize<FarolHello>(Encoding.UTF8.GetString(received.Buffer));
        }
        catch (DecoderFallbackException)
        {
            return;
        }

        if (hello is null || hello.Kind != "farol-hello") return;
        if (!_pairing.Accepts(hello.StoreCodeHash)) return;

        _registry.Observe(hello, received.RemoteEndPoint.Address.ToString(), DateTimeOffset.Now);
    }

    /// <summary>Endereço de broadcast de cada sub-rede IPv4 ativa desta máquina.</summary>
    internal static IReadOnlyList<IPAddress> BroadcastAddresses()
    {
        var addresses = new List<IPAddress>();

        foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            foreach (UnicastIPAddressInformation unicast in nic.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                if (unicast.IPv4Mask is null) continue;

                IPAddress? broadcast = ComputeBroadcast(unicast.Address, unicast.IPv4Mask);
                if (broadcast is not null) addresses.Add(broadcast);
            }
        }

        if (addresses.Count == 0) addresses.Add(IPAddress.Broadcast);
        return addresses;
    }

    internal static IPAddress? ComputeBroadcast(IPAddress address, IPAddress mask)
    {
        byte[] addressBytes = address.GetAddressBytes();
        byte[] maskBytes = mask.GetAddressBytes();
        if (addressBytes.Length != maskBytes.Length) return null;

        var result = new byte[addressBytes.Length];
        for (int i = 0; i < addressBytes.Length; i++)
        {
            result[i] = (byte)(addressBytes[i] | (byte)~maskBytes[i]);
        }

        return new IPAddress(result);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try { _cts?.Cancel(); } catch { }
        _listener?.Dispose();
        _listener = null;

        Task[] loops = new[] { _listenLoop, _announceLoop }.Where(t => t is not null).Select(t => t!).ToArray();
        try { Task.WaitAll(loops, TimeSpan.FromSeconds(2)); } catch { }

        _cts?.Dispose();
        _cts = null;
    }
}
