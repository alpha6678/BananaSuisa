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

        var outcome = await reader.ReadWeightAsync();

        Assert.True(outcome.Reading.IsUsable);
        Assert.Equal(3.140m, outcome.Reading.Weight);
        Assert.True(outcome.Diagnostics.FrameRecognized);

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
        var outcome = await reader.ReadWeightAsync();

        Assert.False(outcome.Reading.HasResponse);
    }

    [Fact]
    public void Reads_frame_delivered_in_fragments()
    {
        byte[] frame = FrameFactory.Delimited("005.250");
        // Entrega o frame em três pedaços separados (simula chegada fragmentada).
        var chunks = new[]
        {
            frame[..2],
            frame[2..5],
            frame[5..],
        };
        var channel = new ScriptedSerialChannel(chunks);
        channel.Open(SerialConfig.Default("COM-TEST"));

        var options = new SerialReadOptions(TotalTimeoutMs: 1000, FirstByteTimeoutMs: 1000, InterByteTimeoutMs: 300);
        var outcome = SerialWeightReader.Read(channel, new ToledoProtocol(), options, CancellationToken.None);

        Assert.True(outcome.Reading.IsUsable);
        Assert.Equal(5.250m, outcome.Reading.Weight);
    }

    [Fact]
    public void Stale_buffer_is_purged_before_request()
    {
        byte[] frame = FrameFactory.Delimited("002.000");
        var channel = new ScriptedSerialChannel(new[] { frame })
        {
            // Fragmento obsoleto de um frame anterior; deve ser descartado na purga.
            InitialStale = new byte[] { SerialControl.STX, (byte)'9', (byte)'9' },
        };
        channel.Open(SerialConfig.Default("COM-TEST"));

        var options = new SerialReadOptions(1000, 1000, 300);
        var outcome = SerialWeightReader.Read(channel, new ToledoProtocol(), options, CancellationToken.None);

        Assert.Equal(2.000m, outcome.Reading.Weight);
    }

    [Fact]
    public void Cancellation_returns_promptly()
    {
        var channel = new ScriptedSerialChannel(Array.Empty<byte[]>());
        channel.Open(SerialConfig.Default("COM-TEST"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var options = new SerialReadOptions(5000, 5000, 300);

        Assert.Throws<OperationCanceledException>(() =>
            SerialWeightReader.Read(channel, new ToledoProtocol(), options, cts.Token));
    }

    [Fact]
    public void Disconnect_surfaces_typed_exception()
    {
        var channel = new ScriptedSerialChannel(Array.Empty<byte[]>())
        {
            ThrowDisconnectAfterReads = 0,
        };
        channel.Open(SerialConfig.Default("COM-TEST"));

        var options = new SerialReadOptions(1000, 1000, 300);

        var ex = Assert.Throws<SerialChannelException>(() =>
            SerialWeightReader.Read(channel, new ToledoProtocol(), options, CancellationToken.None));
        Assert.Equal(SerialFault.Disconnected, ex.Fault);
    }

    [Fact]
    public void Line_error_is_reported_in_diagnostics()
    {
        var channel = new ScriptedSerialChannel(Array.Empty<byte[]>())
        {
            LineErrorOnce = "paridade",
        };
        channel.Open(SerialConfig.Default("COM-TEST"));

        var options = new SerialReadOptions(200, 200, 100);
        var outcome = SerialWeightReader.Read(channel, new ToledoProtocol(), options, CancellationToken.None);

        Assert.False(outcome.Reading.HasResponse);
        Assert.Contains("paridade", outcome.Diagnostics.Reason);
    }
}
