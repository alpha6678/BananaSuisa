using Ribanense.Solucoes.App.Balanca.Domain;
using Ribanense.Solucoes.App.Balanca.Serial;
using Ribanense.Solucoes.App.Balanca.Services;
using Xunit;

namespace Ribanense.Solucoes.App.Balanca.Tests;

public class ScanEngineTests
{
    private static BalancaModel SimulatedModel =>
        BalancaModelRegistry.FindByKey("simulada")!;

    [Fact]
    public void BuildCandidates_puts_model_default_first()
    {
        var engine = new ScanEngine(new RealSerialChannelFactory());
        var model = BalancaModelRegistry.FindByKey("toledo")!;

        var candidates = engine.BuildCandidates(model, new[] { "COM3" }, ScanOptions.Default);

        Assert.NotEmpty(candidates);
        var expected = model.DefaultConfig("COM3");
        Assert.Equal(expected.BaudRate, candidates[0].BaudRate);
        Assert.Equal(expected.DataBits, candidates[0].DataBits);
        Assert.Equal(expected.Parity, candidates[0].Parity);
    }

    [Fact]
    public void BuildCandidates_has_no_duplicates()
    {
        var engine = new ScanEngine(new RealSerialChannelFactory());
        var model = BalancaModelRegistry.FindByKey("toledo")!;

        var candidates = engine.BuildCandidates(model, new[] { "COM1" }, ScanOptions.Default);

        int distinct = candidates
            .Select(c => $"{c.Port}|{c.BaudRate}|{c.DataBits}|{c.Parity}|{c.StopBits}|{c.Handshake}")
            .Distinct()
            .Count();

        Assert.Equal(candidates.Count, distinct);
    }

    [Fact]
    public async Task ScanAll_finds_matching_config_on_simulated_scale()
    {
        var factory = new SimulatedSerialChannelFactory(
            SerialConfig.Default(SimulatedSerialChannelFactory.SimulatedPort), weight: 7.500m);
        var engine = new ScanEngine(factory);

        var ports = factory.ListPorts().Select(p => p.Port).ToList();
        var hits = await engine.ScanAllAsync(SimulatedModel, ports, ScanOptions.Default);

        Assert.NotEmpty(hits);
        Assert.Equal(7.500m, hits[0].Reading.Weight);
        Assert.Equal(WeightStatus.Estavel, hits[0].Reading.Status);
    }

    [Fact]
    public async Task Probe_returns_no_response_for_wrong_baud()
    {
        var factory = new SimulatedSerialChannelFactory(
            SerialConfig.Default(SimulatedSerialChannelFactory.SimulatedPort));
        var engine = new ScanEngine(factory);

        var wrong = SerialConfig.Default(SimulatedSerialChannelFactory.SimulatedPort) with { BaudRate = 1200, TimeoutMs = 300 };
        var result = await engine.ProbeAsync(SimulatedModel, wrong);

        Assert.False(result.Reading.HasResponse);
    }
}
