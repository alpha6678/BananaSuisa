using Ribanense.Solucoes.App.Farol.Analysis;
using Ribanense.Solucoes.App.Farol.Domain;
using Xunit;

namespace Ribanense.Solucoes.App.Farol.Tests;

public class FindingEngineTests
{
    private readonly FindingEngine _engine = new();

    [Fact]
    public void Maquina_saudavel_nao_gera_achados()
    {
        Assert.Empty(_engine.Evaluate(BundleFactory.Healthy()));
    }

    [Fact]
    public void Spooler_parado_e_achado_grave_de_impressao()
    {
        EvidenceBundle bundle = BundleFactory.Healthy() with
        {
            Services = new[]
            {
                new ServiceInfo("Spooler", "Spooler de Impressão", "Stopped", "Automatic"),
            },
        };

        Finding finding = Assert.Single(_engine.Evaluate(bundle));

        Assert.Equal("service.spooler.stopped", finding.RuleId);
        Assert.Equal(FindingSeverity.High, finding.Severity);
        Assert.Equal("Impressão", finding.Category);
        Assert.Contains("Stopped", finding.Evidence, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(5_000_000_000L, FindingSeverity.High)]
    [InlineData(75_000_000_000L, FindingSeverity.Medium)]
    public void Disco_apertado_escala_a_severidade(long freeBytes, FindingSeverity expected)
    {
        EvidenceBundle bundle = BundleFactory.Healthy() with
        {
            Disks = new[] { new DiskInfo(@"C:\", "Sistema", "NTFS", 500_000_000_000, freeBytes) },
        };

        Finding finding = Assert.Single(_engine.Evaluate(bundle));

        Assert.Equal(expected, finding.Severity);
        Assert.Equal("Disco", finding.Category);
    }

    [Fact]
    public void Disco_confortavel_nao_gera_achado()
    {
        EvidenceBundle bundle = BundleFactory.Healthy() with
        {
            Disks = new[] { new DiskInfo(@"C:\", "Sistema", "NTFS", 500_000_000_000, 200_000_000_000) },
        };

        Assert.Empty(_engine.Evaluate(bundle));
    }

    [Fact]
    public void Rede_publica_sem_pares_e_grave()
    {
        EvidenceBundle bundle = WithNetwork(BundleFactory.Healthy(), NetworkCategory.Public);

        Finding finding = Assert.Single(_engine.Evaluate(bundle, Array.Empty<PeerBeacon>()));

        Assert.Equal("network.public-profile", finding.RuleId);
        Assert.Equal(FindingSeverity.High, finding.Severity);
    }

    [Fact]
    public void Rede_publica_com_pares_visiveis_e_apenas_atencao()
    {
        EvidenceBundle bundle = WithNetwork(BundleFactory.Healthy(), NetworkCategory.Public);
        var peers = new[] { new PeerBeacon { MachineId = "caixa-2", LastSeen = DateTimeOffset.Now } };

        Finding finding = Assert.Single(_engine.Evaluate(bundle, peers));

        Assert.Equal(FindingSeverity.Medium, finding.Severity);
    }

    [Fact]
    public void Sem_gateway_e_sem_dns_gera_dois_achados_graves()
    {
        EvidenceBundle bundle = BundleFactory.Healthy() with
        {
            Network = new NetworkInfo(
                NetworkCategory.Private, null, Array.Empty<AdapterInfo>(), Array.Empty<string>(), null, null),
        };

        IReadOnlyList<Finding> findings = _engine.Evaluate(bundle);

        Assert.Contains(findings, f => f.RuleId == "network.no-gateway" && f.Severity == FindingSeverity.High);
        Assert.Contains(findings, f => f.RuleId == "network.no-dns" && f.Severity == FindingSeverity.High);
    }

    [Fact]
    public void Impressora_padrao_offline_e_mais_grave_que_secundaria()
    {
        EvidenceBundle bundle = BundleFactory.Healthy() with
        {
            Printers = new[]
            {
                new PrinterInfo("Termica Balcao", null, "USB001", true, true, true, 0, "Offline"),
                new PrinterInfo("Laser Escritorio", null, "IP_192.168.0.50", false, true, false, 0, "Offline"),
            },
        };

        IReadOnlyList<Finding> findings = _engine.Evaluate(bundle);

        Assert.Equal(FindingSeverity.High, findings.First(f => f.Title.Contains("Termica")).Severity);
        Assert.Equal(FindingSeverity.Medium, findings.First(f => f.Title.Contains("Laser")).Severity);
    }

    [Fact]
    public void Sensor_sem_permissao_vira_achado_informativo_e_nao_derruba_a_analise()
    {
        EvidenceBundle bundle = BundleFactory.Healthy() with
        {
            Collectors = new[]
            {
                new CollectorOutcome("eventlog", "Eventos", CollectorStatus.Denied, "Sem permissão.", 3),
            },
        };

        Finding finding = Assert.Single(_engine.Evaluate(bundle));

        Assert.Equal("collector.incomplete", finding.RuleId);
        Assert.Equal(FindingSeverity.Info, finding.Severity);
    }

    [Fact]
    public void Achados_saem_ordenados_do_mais_grave_para_o_menos()
    {
        EvidenceBundle bundle = BundleFactory.Healthy() with
        {
            Services = new[]
            {
                new ServiceInfo("Spooler", "Spooler", "Stopped", "Automatic"),
                new ServiceInfo("Dnscache", "Cliente DNS", "Stopped", "Automatic"),
            },
            RibanenseApps = new[]
            {
                new RibanenseAppInfo("com.ribanense.winget", "1.0.0", true, 2, "falhou", DateTimeOffset.Now),
            },
        };

        IReadOnlyList<Finding> findings = _engine.Evaluate(bundle);

        Assert.Equal(
            new[] { FindingSeverity.High, FindingSeverity.Medium, FindingSeverity.Low },
            findings.Select(f => f.Severity));
    }

    private static EvidenceBundle WithNetwork(EvidenceBundle bundle, NetworkCategory category) =>
        bundle with
        {
            Network = bundle.Network! with { Category = category },
        };
}
