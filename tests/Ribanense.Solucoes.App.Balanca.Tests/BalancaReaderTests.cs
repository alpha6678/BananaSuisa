using Ribanense.Solucoes.App.Balanca.Domain;
using Ribanense.Solucoes.App.Balanca.Protocols;
using Ribanense.Solucoes.App.Balanca.Serial;
using Ribanense.Solucoes.App.Balanca.Services;
using Xunit;

namespace Ribanense.Solucoes.App.Balanca.Tests;

public class BalancaReaderTests
{
    [Fact]
    public async Task Reads_weight_from_simulated_scale()
    {
        var target = SerialConfig.Default(SimulatedSerialChannelFactory.SimulatedPort);
        var factory = new SimulatedSerialChannelFactory(target, weight: 3.140m);
        using var reader = new BalancaReader(factory);

        reader.Activate(target with { TimeoutMs = 500 }, new GenericHeuristicProtocol());
        Assert.True(reader.IsActive);

        var reading = await reader.ReadWeightAsync();

        Assert.True(reading.IsUsable);
        Assert.Equal(3.140m, reading.Weight);

        reader.Deactivate();
        Assert.False(reader.IsActive);
    }

    [Fact]
    public async Task Wrong_config_yields_no_reading()
    {
        var target = SerialConfig.Default(SimulatedSerialChannelFactory.SimulatedPort);
        var factory = new SimulatedSerialChannelFactory(target, weight: 3.140m);
        using var reader = new BalancaReader(factory);

        reader.Activate(target with { BaudRate = 4800, TimeoutMs = 300 }, new GenericHeuristicProtocol());
        var reading = await reader.ReadWeightAsync();

        Assert.False(reading.HasResponse);
    }
}
