using System.IO;
using System.IO.Compression;
using Ribanense.Solucoes.App.Farol.Analysis;
using Ribanense.Solucoes.App.Farol.Collectors;
using Ribanense.Solucoes.App.Farol.Domain;
using Ribanense.Solucoes.App.Farol.Services;
using Xunit;

namespace Ribanense.Solucoes.App.Farol.Tests;

/// <summary>Diretório temporário que se limpa sozinho no fim do teste.</summary>
public sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "farol-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch (IOException) { }
    }
}

public class BundleStoreTests
{
    [Fact]
    public void Dossie_salvo_pode_ser_relido_pelo_id()
    {
        using var temp = new TempDirectory();
        var store = new BundleStore(temp.Path);
        EvidenceBundle bundle = BundleFactory.Healthy();

        store.Save(bundle);

        Assert.Equal(bundle.MachineName, store.GetById(bundle.Id)?.MachineName);
    }

    [Fact]
    public void GetLatest_devolve_a_captura_mais_recente()
    {
        using var temp = new TempDirectory();
        var store = new BundleStore(temp.Path);

        var older = BundleFactory.Healthy("CAIXA-1") with { CapturedAt = DateTimeOffset.Now.AddHours(-2) };
        var newer = BundleFactory.Healthy("CAIXA-2") with { CapturedAt = DateTimeOffset.Now };

        store.Save(older);
        store.Save(newer);

        Assert.Equal(newer.Id, store.GetLatest()?.Id);
    }

    [Fact]
    public void Historico_e_podado_pela_retencao()
    {
        using var temp = new TempDirectory();
        var store = new BundleStore(temp.Path, retention: 3);

        for (int i = 0; i < 6; i++)
        {
            store.Save(BundleFactory.Healthy() with { CapturedAt = DateTimeOffset.Now.AddMinutes(-i) });
        }

        Assert.Equal(3, store.List().Count);
    }

    [Fact]
    public void Diretorio_vazio_nao_tem_dossie()
    {
        using var temp = new TempDirectory();

        Assert.Null(new BundleStore(temp.Path).GetLatest());
    }
}

public class BundleExporterTests
{
    [Fact]
    public void Pacote_contem_dossie_achados_timeline_e_resumo()
    {
        using var temp = new TempDirectory();

        EvidenceBundle bundle = BundleFactory.Healthy();
        var findings = new FindingEngine().Evaluate(bundle);
        string destination = Path.Combine(temp.Path, BundleExporter.SuggestFileName(bundle));

        new BundleExporter().Export(destination, bundle, findings, Array.Empty<PeerBeacon>());

        using ZipArchive archive = ZipFile.OpenRead(destination);

        Assert.Equal(
            new[] { "bundle.json", "findings.json", "peers-timeline.json", "resumo.txt" },
            archive.Entries.Select(e => e.FullName).Order());
    }

    [Fact]
    public void Nome_sugerido_usa_hostname_e_carimbo_de_tempo()
    {
        EvidenceBundle bundle = BundleFactory.Healthy("CAIXA-1") with
        {
            CapturedAt = new DateTimeOffset(2026, 8, 8, 14, 30, 0, TimeSpan.Zero),
        };

        Assert.Equal("farol-CAIXA-1-20260808-1430.zip", BundleExporter.SuggestFileName(bundle));
    }
}

public class FirewallRuleInstallerTests
{
    [Fact]
    public void Script_de_instalacao_restringe_perfil_e_sub_rede()
    {
        string script = FirewallRuleInstaller.BuildInstallScript(38400, 38401);

        Assert.Contains("-Profile Domain,Private", script, StringComparison.Ordinal);
        Assert.Contains("-RemoteAddress LocalSubnet", script, StringComparison.Ordinal);
        Assert.Contains("-Direction Inbound", script, StringComparison.Ordinal);
        Assert.Contains("Port = 38400", script, StringComparison.Ordinal);
        Assert.Contains("Port = 38401", script, StringComparison.Ordinal);
        Assert.DoesNotContain("%UDP_PORT%", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Script_de_remocao_cita_as_duas_regras()
    {
        string script = FirewallRuleInstaller.BuildRemoveScript();

        Assert.Contains(FirewallRuleInstaller.DiscoveryRuleName, script, StringComparison.Ordinal);
        Assert.Contains(FirewallRuleInstaller.PeerRuleName, script, StringComparison.Ordinal);
    }
}

public class BundleCollectorTests
{
    private sealed class ThrowingCollector : ICollector
    {
        private readonly Exception _failure;

        public ThrowingCollector(string id, Exception failure)
        {
            Id = id;
            _failure = failure;
        }

        public string Id { get; }
        public string DisplayName => Id;

        public Task CollectAsync(EvidenceBundleBuilder builder, CancellationToken ct) =>
            Task.FromException(_failure);
    }

    private static readonly FarolIdentitySnapshot Identity =
        new("machine-1", "CAIXA-1", "Caixa 1", "0.1.0");

    [Fact]
    public async Task Sensor_que_falha_nao_derruba_os_demais()
    {
        var collector = new BundleCollector(new ICollector[]
        {
            new ThrowingCollector("quebrado", new InvalidOperationException("boom")),
            new IdentityCollector(),
        });

        EvidenceBundle bundle = await collector.CaptureAsync(Identity, CancellationToken.None);

        Assert.Equal(CollectorStatus.Failed, bundle.Collectors.Single(c => c.CollectorId == "quebrado").Status);
        Assert.Equal(CollectorStatus.Ok, bundle.Collectors.Single(c => c.CollectorId == "identity").Status);
        Assert.NotNull(bundle.Identity);
    }

    public static TheoryData<Exception, CollectorStatus> ToleratedFailures => new()
    {
        { new CollectorDeniedException("sem acesso"), CollectorStatus.Denied },
        { new UnauthorizedAccessException("sem acesso"), CollectorStatus.Denied },
        { new PlatformNotSupportedException("indisponível aqui"), CollectorStatus.Skipped },
    };

    [Theory]
    [MemberData(nameof(ToleratedFailures))]
    public async Task Falta_de_permissao_e_registrada_sem_virar_erro(Exception failure, CollectorStatus expected)
    {
        var collector = new BundleCollector(new ICollector[] { new ThrowingCollector("sensor", failure) });

        EvidenceBundle bundle = await collector.CaptureAsync(Identity, CancellationToken.None);

        Assert.Equal(expected, bundle.Collectors.Single().Status);
    }

    [Fact]
    public async Task Dossie_carrega_a_identidade_informada()
    {
        var collector = new BundleCollector(Array.Empty<ICollector>());

        EvidenceBundle bundle = await collector.CaptureAsync(Identity, CancellationToken.None);

        Assert.Equal("machine-1", bundle.MachineId);
        Assert.Equal("Caixa 1", bundle.FriendlyName);
        Assert.Equal(EvidenceBundle.CurrentSchemaVersion, bundle.SchemaVersion);
    }
}

public class RuleBasedExplainerTests
{
    [Fact]
    public void Sem_achados_o_resumo_diz_que_esta_tudo_bem()
    {
        string text = RuleBasedExplainer.Explain(BundleFactory.Healthy(), Array.Empty<Finding>());

        Assert.Contains("Nenhum problema conhecido", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Resumo_agrupa_por_severidade_e_traz_a_sugestao()
    {
        EvidenceBundle bundle = BundleFactory.Healthy() with
        {
            Services = new[] { new ServiceInfo("Spooler", "Spooler", "Stopped", "Automatic") },
        };

        var findings = new FindingEngine().Evaluate(bundle);
        string text = RuleBasedExplainer.Explain(bundle, findings);

        Assert.Contains("Grave:", text, StringComparison.Ordinal);
        Assert.Contains("O que fazer:", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Manchete_destaca_o_achado_mais_grave()
    {
        var findings = new[]
        {
            new Finding("a", FindingSeverity.High, "Impressão", "Fila de impressão parada", "e", "s"),
            new Finding("b", FindingSeverity.Medium, "Rede", "DNS divergente", "e", "s"),
        };

        Assert.Contains("fila de impressão parada", RuleBasedExplainer.Headline(findings), StringComparison.Ordinal);
    }

    [Fact]
    public void Manchete_sem_achados_e_neutra()
    {
        Assert.Equal("Sem problemas detectados.", RuleBasedExplainer.Headline(Array.Empty<Finding>()));
    }
}

public class EventLogCollectorTests
{
    [Fact]
    public void Quebras_e_indentacao_viram_um_unico_espaco()
    {
        Assert.Equal(
            "linha 1 linha 2",
            EventLogCollector.Truncate("  linha 1\r\n\t linha 2  ", 100));
    }

    [Fact]
    public void Mensagem_acima_do_limite_ganha_reticencias()
    {
        Assert.Equal("abcde…", EventLogCollector.Truncate("abcdefgh", 5));
    }

    [Fact]
    public void Mensagem_vazia_vira_string_vazia()
    {
        Assert.Equal(string.Empty, EventLogCollector.Truncate("   ", 10));
    }
}
