using System.Net;
using System.Net.Sockets;
using System.Text;
using Ribanense.Solucoes.App.Farol.Domain;
using Ribanense.Solucoes.App.Farol.Mesh;
using Xunit;

namespace Ribanense.Solucoes.App.Farol.Tests;

/// <summary>
/// Recepção de anúncios sobre socket UDP real. O cálculo de broadcast já tem
/// teste próprio, mas o que faz um farol aparecer no mapa do vizinho é o
/// caminho escutar → desserializar → filtrar pelo código da loja → registrar,
/// e ele só existe dentro de um socket vivo.
/// </summary>
/// <remarks>
/// O datagrama vai direto para 127.0.0.1 em vez de broadcast: a entrega por
/// sub-rede depende de adaptador ativo e tornaria o teste refém da máquina.
/// </remarks>
public class DiscoveryRoundTripTests
{
    private const string StoreCode = "LOJA-RIBA-042";

    private static int FreeUdpPort()
    {
        using var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
    }

    private static (DiscoveryResponder Responder, PeerRegistry Registry) StartListener(int port)
    {
        var vault = new FakeVault();
        var pairing = new PairingStore(vault);
        pairing.Pair(StoreCode);

        var registry = new PeerRegistry(pairing.MachineId);
        var responder = new DiscoveryResponder(
            registry,
            pairing,
            () => new FarolHello { MachineId = pairing.MachineId, PeerPort = 38401 },
            port);

        responder.Start();
        Assert.True(responder.IsRunning);

        return (responder, registry);
    }

    private static async Task SendHelloAsync(int port, FarolHello hello)
    {
        using var sender = new UdpClient(AddressFamily.InterNetwork);
        byte[] payload = Encoding.UTF8.GetBytes(FarolJson.Serialize(hello, indented: false));

        await sender.SendAsync(payload, new IPEndPoint(IPAddress.Loopback, port));
    }

    /// <summary>Espera ativa curta: a entrega é assíncrona, mas em loopback é imediata.</summary>
    private static async Task<IReadOnlyList<PeerBeacon>> WaitForPeersAsync(
        PeerRegistry registry, int expected)
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            IReadOnlyList<PeerBeacon> peers = registry.Snapshot();
            if (peers.Count >= expected) return peers;

            await Task.Delay(20);
        }

        return registry.Snapshot();
    }

    [Fact]
    public async Task Anuncio_da_mesma_loja_faz_o_par_aparecer_no_mapa()
    {
        int port = FreeUdpPort();
        (DiscoveryResponder responder, PeerRegistry registry) = StartListener(port);

        using (responder)
        {
            await SendHelloAsync(port, new FarolHello
            {
                MachineId = "caixa-1",
                MachineName = "CAIXA-1",
                FriendlyName = "Caixa 1",
                StoreCodeHash = PairingStore.Hash(StoreCode),
                PeerPort = 38401,
            });

            PeerBeacon peer = Assert.Single(await WaitForPeersAsync(registry, 1));

            Assert.Equal("caixa-1", peer.MachineId);
            Assert.Equal("Caixa 1", peer.FriendlyName);
            Assert.Equal("127.0.0.1", peer.Address);
        }
    }

    [Fact]
    public async Task Anuncio_de_outra_loja_e_descartado_na_escuta()
    {
        int port = FreeUdpPort();
        (DiscoveryResponder responder, PeerRegistry registry) = StartListener(port);

        using (responder)
        {
            await SendHelloAsync(port, new FarolHello
            {
                MachineId = "intruso",
                StoreCodeHash = PairingStore.Hash("LOJA-INTRUSA"),
                PeerPort = 38401,
            });

            await Task.Delay(200);

            Assert.Empty(registry.Snapshot());
        }
    }

    /// <summary>
    /// Um pacote qualquer na porta não pode derrubar a escuta: o farol precisa
    /// continuar enxergando os vizinhos depois do lixo.
    /// </summary>
    [Fact]
    public async Task Pacote_invalido_nao_mata_a_escuta()
    {
        int port = FreeUdpPort();
        (DiscoveryResponder responder, PeerRegistry registry) = StartListener(port);

        using (responder)
        {
            using (var noise = new UdpClient(AddressFamily.InterNetwork))
            {
                byte[] garbage = [0xFF, 0x00, 0x13];
                await noise.SendAsync(garbage, new IPEndPoint(IPAddress.Loopback, port));
            }

            await SendHelloAsync(port, new FarolHello
            {
                MachineId = "caixa-2",
                StoreCodeHash = PairingStore.Hash(StoreCode),
                PeerPort = 38401,
            });

            Assert.Single(await WaitForPeersAsync(registry, 1));
        }
    }
}
