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
        Assert.Equal(expected.StopBits, candidates[0].StopBits);
    }

    [Fact]
    public void BuildCandidates_stays_on_the_chosen_port()
    {
        // Caminho padrão do app: a varredura de apoio só testa a porta escolhida no
        // passo 1, sem mexer na maquininha TEF ligada na COM ao lado.
        var engine = new ScanEngine(new RealSerialChannelFactory());
        var model = BalancaModelRegistry.FindByKey("filizola")!;

        var candidates = engine.BuildCandidates(model, new[] { "COM5" }, ScanOptions.Default);

        Assert.NotEmpty(candidates);
        Assert.All(candidates, c => Assert.Equal("COM5", c.Port));
    }

    [Fact]
    public void BuildCandidates_covers_every_port_when_asked()
    {
        var engine = new ScanEngine(new RealSerialChannelFactory());
        var model = BalancaModelRegistry.FindByKey("filizola")!;

        var candidates = engine.BuildCandidates(model, new[] { "COM3", "COM5" }, ScanOptions.Default);

        Assert.Contains(candidates, c => c.Port == "COM3");
        Assert.Contains(candidates, c => c.Port == "COM5");
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
    public void Normal_scan_is_bounded_and_deep_is_much_larger()
    {
        var engine = new ScanEngine(new RealSerialChannelFactory());
        var model = BalancaModelRegistry.FindByKey("toledo")!;

        var normal = engine.BuildCandidates(model, new[] { "COM3" }, ScanOptions.Default);
        var deep = engine.BuildCandidates(model, new[] { "COM3" }, new ScanOptions { Deep = true });

        // Modo normal guiado pelo protocolo é enxuto (não é o produto cartesiano).
        Assert.True(normal.Count <= 20, $"normal={normal.Count}");
        Assert.True(deep.Count > normal.Count * 10);
    }

    [Fact]
    public void Low_baud_gets_larger_timeout_budget()
    {
        var engine = new ScanEngine(new RealSerialChannelFactory());
        var model = BalancaModelRegistry.FindByKey("toledo")!;
        var options = new ScanOptions { TimeoutMsPerAttempt = 1000, Deep = true };

        var candidates = engine.BuildCandidates(model, new[] { "COM3" }, options);

        var low = candidates.First(c => c.BaudRate == 300);
        var high = candidates.First(c => c.BaudRate == 9600);
        Assert.True(low.TimeoutMs > high.TimeoutMs);
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

    [Fact]
    public async Task Probe_reports_busy_port_as_typed_error()
    {
        var factory = new FakeChannelFactory(() => new BusyChannel());
        var engine = new ScanEngine(factory);
        var model = BalancaModelRegistry.FindByKey("toledo")!;

        var result = await engine.ProbeAsync(model, model.DefaultConfig("COM-TEST") with { TimeoutMs = 200 });

        Assert.False(result.Success);
        Assert.Contains("ocupada", result.Error);
    }

    private sealed class BusyChannel : ISerialChannel
    {
        public bool IsOpen => false;
        public void Open(SerialConfig config) =>
            throw new SerialChannelException(SerialFault.Busy, "Porta ocupada (teste).");
        public void Write(ReadOnlySpan<byte> data) { }
        public int Read(byte[] buffer, int offset, int count) => 0;
        public void DiscardInBuffer() { }
        public void Close() { }
        public void Dispose() { }
    }
}
