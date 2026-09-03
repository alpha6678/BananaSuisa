using System.Net;
using System.Net.Sockets;
using Ribanense.Solucoes.App.Farol.Domain;
using Ribanense.Solucoes.App.Farol.Mesh;
using Xunit;

namespace Ribanense.Solucoes.App.Farol.Tests;

/// <summary>
/// Ida e volta sobre socket real. O Farol implementa HTTP/1.1 à mão sobre
/// <c>TcpListener</c>, então a leitura da requisição e a escrita da resposta
/// precisam ser exercitadas de verdade — um erro de cabeçalho aqui só
/// apareceria em produção.
/// </summary>
public class PeerRoundTripTests
{
    private sealed class StubData : IPeerDataSource
    {
        public EvidenceBundle Bundle { get; } = BundleFactory.Healthy("CAIXA-1");

        public HealthSignal GetHealth() => new()
        {
            MachineId = "caixa-1",
            MachineName = "CAIXA-1",
            FriendlyName = "Caixa 1",
            Level = HealthLevel.Degradado,
            MediumFindings = 1,
            Headline = "Um ponto de atenção: dns divergente.",
        };

        public EvidenceBundle? GetLatestBundle() => Bundle;

        public EvidenceBundle? GetBundle(Guid id) => id == Bundle.Id ? Bundle : null;
    }

    /// <summary>Rede local nunca deve demorar; travar aqui é bug, não lentidão.</summary>
    private static CancellationToken Timeout =>
        new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token;

    private static int FreeTcpPort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private static PeerBeacon Beacon(int port) => new()
    {
        MachineId = "caixa-1",
        MachineName = "CAIXA-1",
        Address = "127.0.0.1",
        PeerPort = port,
    };

    [Fact]
    public async Task Health_e_dossie_trafegam_entre_dois_faroes_pareados()
    {
        int port = FreeTcpPort();

        var serverPairing = new PairingStore(new FakeVault());
        serverPairing.Pair("LOJA-RIBA-042");

        var data = new StubData();
        using var server = new PeerHttpServer(serverPairing, data, port);
        server.Start();
        Assert.True(server.IsRunning, server.StartupError);

        var clientPairing = new PairingStore(new FakeVault());
        clientPairing.Pair("loja-riba-042");
        using var client = new PeerClient(clientPairing);

        HealthSignal? health = await client.GetHealthAsync(Beacon(port), Timeout);
        Assert.NotNull(health);
        Assert.Equal(HealthLevel.Degradado, health.Level);
        Assert.Equal("Caixa 1", health.FriendlyName);

        EvidenceBundle? bundle = await client.GetLatestBundleAsync(Beacon(port), Timeout);
        Assert.NotNull(bundle);
        Assert.Equal(data.Bundle.Id, bundle.Id);
        Assert.Equal("Spooler", bundle.Services[0].Name);
    }

    [Fact]
    public async Task Farol_de_outra_loja_nao_consegue_ler_nada()
    {
        int port = FreeTcpPort();

        var serverPairing = new PairingStore(new FakeVault());
        serverPairing.Pair("LOJA-RIBA-042");

        using var server = new PeerHttpServer(serverPairing, new StubData(), port);
        server.Start();

        var intruderPairing = new PairingStore(new FakeVault());
        intruderPairing.Pair("LOJA-INTRUSA");
        using var intruder = new PeerClient(intruderPairing);

        Assert.Null(await intruder.GetHealthAsync(Beacon(port), Timeout));
        Assert.Null(await intruder.GetLatestBundleAsync(Beacon(port), Timeout));
    }

    [Fact]
    public async Task Par_inalcancavel_devolve_nulo_em_vez_de_explodir()
    {
        var pairing = new PairingStore(new FakeVault());
        pairing.Pair("LOJA-RIBA-042");
        using var client = new PeerClient(pairing);

        Assert.Null(await client.GetHealthAsync(Beacon(FreeTcpPort()), Timeout));
    }
}
