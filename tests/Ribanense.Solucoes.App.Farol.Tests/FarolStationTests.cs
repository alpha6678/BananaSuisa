using Ribanense.Solucoes.App.Farol.Analysis;
using Ribanense.Solucoes.App.Farol.Collectors;
using Ribanense.Solucoes.App.Farol.Domain;
using Ribanense.Solucoes.App.Farol.Mesh;
using Ribanense.Solucoes.App.Farol.Services;
using Ribanense.Solucoes.PluginSDK.Logging;
using Xunit;

namespace Ribanense.Solucoes.App.Farol.Tests;

public sealed class RecordingLog : IAppJsonLog
{
    public List<string> Messages { get; } = new();

    public void Write(
        AppLogLevel level,
        string category,
        string message,
        Exception? exception = null,
        IDictionary<string, string>? data = null) => Messages.Add($"{category}: {message}");
}

/// <summary>
/// Portão da malha e ciclo de captura da estação.
/// </summary>
/// <remarks>
/// O portão importa mais do que parece: um farol sem código da loja não pode
/// abrir porta nenhuma, e essa decisão fica só aqui. Se ela vazar, uma máquina
/// recém-instalada passa a escutar a rede antes de o usuário consentir.
/// </remarks>
public class FarolStationTests
{
    private static FarolStation Build(
        TempDirectory temp, PairingStore pairing, out RecordingLog log, params ICollector[] collectors)
    {
        log = new RecordingLog();

        return new FarolStation(
            pairing,
            new BundleStore(temp.Path),
            new BundleCollector(collectors),
            new FindingEngine(),
            log,
            "0.1.0");
    }

    [Fact]
    public void Farol_sem_codigo_da_loja_nao_abre_porta_alguma()
    {
        using var temp = new TempDirectory();
        var pairing = new PairingStore(new FakeVault());

        using FarolStation station = Build(temp, pairing, out RecordingLog log);
        station.StartMesh();

        Assert.False(station.MeshRunning);
        Assert.DoesNotContain(log.Messages, m => m.StartsWith("mesh:", StringComparison.Ordinal));
    }

    [Fact]
    public void Malha_desligada_nos_ajustes_continua_muda_mesmo_pareada()
    {
        using var temp = new TempDirectory();
        var pairing = new PairingStore(new FakeVault());
        pairing.Pair("LOJA-RIBA-042");
        pairing.MeshEnabled = false;

        using FarolStation station = Build(temp, pairing, out _);
        station.StartMesh();

        Assert.False(station.MeshRunning);
    }

    [Fact]
    public async Task Captura_publica_o_dossie_para_os_pares_e_avisa_a_interface()
    {
        using var temp = new TempDirectory();
        var pairing = new PairingStore(new FakeVault());

        using FarolStation station = Build(temp, pairing, out _, new IdentityCollector());

        CaptureResult? notified = null;
        station.Captured += result => notified = result;

        Assert.Equal(HealthLevel.Desconhecido, station.GetHealth().Level);

        CaptureResult captured = await station.CaptureAsync(CancellationToken.None);

        Assert.NotNull(notified);
        Assert.Equal(captured.Bundle.Id, notified.Bundle.Id);

        // O que o par lê pelo HTTP tem de ser o mesmo dossiê recém-capturado.
        Assert.Equal(captured.Bundle.Id, station.GetLatestBundle()!.Id);
        Assert.Equal(captured.Bundle.Id, station.GetBundle(captured.Bundle.Id)!.Id);
        Assert.Equal(captured.Bundle.Id, station.GetHealth().LastBundleId);
        Assert.NotEqual(HealthLevel.Desconhecido, station.GetHealth().Level);
    }

    [Fact]
    public void Estacao_reabre_com_o_ultimo_dossie_do_disco()
    {
        using var temp = new TempDirectory();
        var pairing = new PairingStore(new FakeVault());

        EvidenceBundle saved = BundleFactory.Healthy();
        new BundleStore(temp.Path).Save(saved);

        using FarolStation station = Build(temp, pairing, out _);

        Assert.Equal(saved.Id, station.LatestBundle?.Id);
        Assert.Equal(saved.Id, station.GetHealth().LastBundleId);
    }
}
