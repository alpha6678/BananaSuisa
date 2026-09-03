using Ribanense.Solucoes.App.Farol.Analysis;
using Ribanense.Solucoes.App.Farol.Domain;
using Xunit;

namespace Ribanense.Solucoes.App.Farol.Tests;

public class BundleComparerTests
{
    private readonly BundleComparer _comparer = new();

    [Fact]
    public void Maquinas_iguais_nao_produzem_diferencas()
    {
        Assert.Empty(_comparer.Compare(BundleFactory.Healthy("CAIXA-1"), BundleFactory.Healthy("CAIXA-2")));
    }

    [Fact]
    public void Servico_parado_so_aqui_e_a_diferenca_mais_grave()
    {
        EvidenceBundle local = BundleFactory.Healthy("CAIXA-2") with
        {
            Services = new[]
            {
                new ServiceInfo("Spooler", "Spooler de Impressão", "Stopped", "Automatic"),
            },
        };

        BundleDifference difference = _comparer
            .Compare(local, BundleFactory.Healthy("CAIXA-1"))
            .First();

        Assert.Equal("Serviços", difference.Category);
        Assert.Equal(FindingSeverity.High, difference.Severity);
        Assert.Equal("Stopped", difference.Local);
        Assert.Equal("Running", difference.Remote);
    }

    [Fact]
    public void Dns_divergente_aparece_com_os_dois_lados()
    {
        EvidenceBundle remote = BundleFactory.Healthy("CAIXA-1");
        EvidenceBundle local = BundleFactory.Healthy("CAIXA-2") with
        {
            Network = remote.Network! with { ActiveDnsServers = new[] { "8.8.8.8" } },
        };

        BundleDifference difference = Assert.Single(
            _comparer.Compare(local, remote),
            d => d.Aspect == "Servidores DNS");

        Assert.Equal("8.8.8.8", difference.Local);
        Assert.Equal("192.168.0.1", difference.Remote);
        Assert.Equal(FindingSeverity.Medium, difference.Severity);
    }

    [Fact]
    public void Impressora_presente_so_no_irmao_e_diferenca_grave()
    {
        EvidenceBundle local = BundleFactory.Healthy("CAIXA-2") with
        {
            Printers = Array.Empty<PrinterInfo>(),
        };

        BundleDifference difference = Assert.Single(_comparer.Compare(local, BundleFactory.Healthy("CAIXA-1")));

        Assert.Equal("Impressão", difference.Category);
        Assert.Equal(FindingSeverity.High, difference.Severity);
        Assert.Equal("ausente", difference.Local);
    }

    [Fact]
    public void Impressora_extra_local_e_apenas_informativa()
    {
        EvidenceBundle remote = BundleFactory.Healthy("CAIXA-1") with
        {
            Printers = Array.Empty<PrinterInfo>(),
        };

        BundleDifference difference = Assert.Single(_comparer.Compare(BundleFactory.Healthy("CAIXA-2"), remote));

        Assert.Equal(FindingSeverity.Info, difference.Severity);
    }

    [Fact]
    public void Disco_apertado_no_irmao_tambem_nao_gera_diferenca()
    {
        var tight = new[] { new DiskInfo(@"C:\", "Sistema", "NTFS", 500_000_000_000, 5_000_000_000) };

        EvidenceBundle local = BundleFactory.Healthy("CAIXA-2") with { Disks = tight };
        EvidenceBundle remote = BundleFactory.Healthy("CAIXA-1") with { Disks = tight };

        Assert.Empty(_comparer.Compare(local, remote));
    }

    [Fact]
    public void Perfil_publico_local_e_privado_remoto_e_grave()
    {
        EvidenceBundle remote = BundleFactory.Healthy("CAIXA-1");
        EvidenceBundle local = BundleFactory.Healthy("CAIXA-2") with
        {
            Network = remote.Network! with { Category = NetworkCategory.Public },
        };

        BundleDifference difference = Assert.Single(
            _comparer.Compare(local, remote),
            d => d.Aspect == "Perfil de rede");

        Assert.Equal(FindingSeverity.High, difference.Severity);
    }
}
