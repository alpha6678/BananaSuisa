using System.Net;
using Ribanense.Solucoes.App.Farol.Domain;
using Ribanense.Solucoes.App.Farol.Mesh;
using Xunit;

namespace Ribanense.Solucoes.App.Farol.Tests;

public class PairingStoreTests
{
    [Fact]
    public void Codigo_e_normalizado_antes_do_hash()
    {
        Assert.Equal(
            PairingStore.Hash("loja-riba-042"),
            PairingStore.Hash("  LOJA-RIBA-042 "));
    }

    [Fact]
    public void Codigos_diferentes_geram_hashes_diferentes()
    {
        Assert.NotEqual(PairingStore.Hash("LOJA-A"), PairingStore.Hash("LOJA-B"));
    }

    [Fact]
    public void Farol_sem_pareamento_recusa_qualquer_hash()
    {
        var store = new PairingStore(new FakeVault());

        Assert.False(store.IsPaired);
        Assert.False(store.Accepts(PairingStore.Hash("LOJA-A")));
    }

    [Fact]
    public void Aceita_somente_o_hash_do_proprio_codigo()
    {
        var store = new PairingStore(new FakeVault());
        store.Pair("LOJA-RIBA-042");

        Assert.True(store.Accepts(PairingStore.Hash("loja-riba-042")));
        Assert.False(store.Accepts(PairingStore.Hash("LOJA-OUTRA")));
        Assert.False(store.Accepts(null));
        Assert.False(store.Accepts(string.Empty));
    }

    [Fact]
    public void MachineId_e_estavel_entre_leituras()
    {
        var store = new PairingStore(new FakeVault());

        Assert.Equal(store.MachineId, store.MachineId);
    }

    [Fact]
    public void Remover_pareamento_desliga_a_troca_de_dossies()
    {
        var store = new PairingStore(new FakeVault());
        store.Pair("LOJA-RIBA-042");
        store.Unpair();

        Assert.False(store.IsPaired);
        Assert.Null(store.StoreCodeHash);
    }
}

public class PeerRegistryTests
{
    private static FarolHello Hello(string machineId) => new()
    {
        MachineId = machineId,
        MachineName = machineId.ToUpperInvariant(),
        FriendlyName = machineId,
        PeerPort = 38401,
    };

    [Fact]
    public void Registro_ignora_o_proprio_anuncio()
    {
        var registry = new PeerRegistry("eu");

        Assert.False(registry.Observe(Hello("eu"), "192.168.0.10", DateTimeOffset.Now));
        Assert.Empty(registry.Snapshot());
    }

    [Fact]
    public void Segundo_anuncio_atualiza_em_vez_de_duplicar()
    {
        var registry = new PeerRegistry("eu");
        var now = DateTimeOffset.Now;

        registry.Observe(Hello("caixa-1"), "192.168.0.11", now);
        registry.Observe(Hello("caixa-1"), "192.168.0.12", now.AddSeconds(30));

        PeerBeacon peer = Assert.Single(registry.Snapshot());
        Assert.Equal("192.168.0.12", peer.Address);
    }

    [Theory]
    [InlineData(0, PeerState.Online)]
    [InlineData(2, PeerState.Ausente)]
    [InlineData(10, PeerState.Offline)]
    public void Silencio_move_o_par_de_online_para_offline(int minutesSilent, PeerState expected)
    {
        var now = DateTimeOffset.Now;
        var peer = new PeerBeacon { MachineId = "caixa-1", LastSeen = now.AddMinutes(-minutesSilent) };

        Assert.Equal(expected, peer.StateAt(now, PeerRegistry.AbsentAfter, PeerRegistry.OfflineAfter));
    }

    [Fact]
    public void Par_offline_mantem_o_ultimo_sinal_de_saude()
    {
        var registry = new PeerRegistry("eu");
        registry.Observe(Hello("caixa-1"), "192.168.0.11", DateTimeOffset.Now.AddMinutes(-30));
        registry.AttachHealth("caixa-1", new HealthSignal { Level = HealthLevel.Ok, Headline = "tudo certo" });

        PeerBeacon peer = Assert.Single(registry.Snapshot());

        Assert.Equal("tudo certo", peer.LastHealth!.Headline);
    }
}

public class DiscoveryResponderTests
{
    [Theory]
    [InlineData("192.168.0.10", "255.255.255.0", "192.168.0.255")]
    [InlineData("10.1.2.3", "255.255.0.0", "10.1.255.255")]
    [InlineData("172.16.5.9", "255.255.255.128", "172.16.5.127")]
    public void Broadcast_e_calculado_por_sub_rede(string address, string mask, string expected)
    {
        IPAddress? broadcast = DiscoveryResponder.ComputeBroadcast(
            IPAddress.Parse(address), IPAddress.Parse(mask));

        Assert.Equal(expected, broadcast?.ToString());
    }
}

public class PeerHttpServerTests
{
    private sealed class StubData : IPeerDataSource
    {
        public EvidenceBundle Bundle { get; } = BundleFactory.Healthy();

        public HealthSignal GetHealth() => new() { MachineName = "CAIXA-1", Level = HealthLevel.Ok };

        public EvidenceBundle? GetLatestBundle() => Bundle;

        public EvidenceBundle? GetBundle(Guid id) => id == Bundle.Id ? Bundle : null;
    }

    private static (PeerHttpServer Server, StubData Data, string Hash) Build()
    {
        var pairing = new PairingStore(new FakeVault());
        pairing.Pair("LOJA-RIBA-042");

        var data = new StubData();
        return (new PeerHttpServer(pairing, data, 0), data, pairing.StoreCodeHash!);
    }

    private static PeerRequest Request(string path, string? hash) =>
        new("GET", path, hash is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { [PeerHttpServer.StoreHeader] = hash });

    [Fact]
    public void Requisicao_sem_codigo_da_loja_e_recusada()
    {
        (PeerHttpServer server, _, _) = Build();

        Assert.Equal(403, server.Handle(Request("/health", null)).StatusCode);
    }

    [Fact]
    public void Requisicao_com_codigo_errado_e_recusada()
    {
        (PeerHttpServer server, _, _) = Build();

        Assert.Equal(403, server.Handle(Request("/health", PairingStore.Hash("LOJA-INTRUSA"))).StatusCode);
    }

    [Fact]
    public void Health_responde_json_com_o_sinal_leve()
    {
        (PeerHttpServer server, _, string hash) = Build();

        PeerResponse response = server.Handle(Request("/health", hash));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(HealthLevel.Ok, FarolJson.Deserialize<HealthSignal>(response.Body)!.Level);
    }

    [Fact]
    public void Bundle_latest_devolve_o_dossie_completo()
    {
        (PeerHttpServer server, StubData data, string hash) = Build();

        PeerResponse response = server.Handle(Request("/bundle/latest", hash));

        Assert.Equal(data.Bundle.Id, FarolJson.Deserialize<EvidenceBundle>(response.Body)!.Id);
    }

    [Fact]
    public void Bundle_por_id_desconhecido_responde_sem_conteudo()
    {
        (PeerHttpServer server, _, string hash) = Build();

        Assert.Equal(204, server.Handle(Request($"/bundle/{Guid.NewGuid():D}", hash)).StatusCode);
    }

    [Fact]
    public void Rota_desconhecida_responde_404()
    {
        (PeerHttpServer server, _, string hash) = Build();

        Assert.Equal(404, server.Handle(Request("/settings", hash)).StatusCode);
    }
}
