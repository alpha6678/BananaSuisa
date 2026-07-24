using Ribanense.Solucoes.App.Balanca.Domain;
using Ribanense.Solucoes.App.Balanca.Protocols;
using Xunit;

namespace Ribanense.Solucoes.App.Balanca.Tests;

public class ProtocolTests
{
    [Fact]
    public void Toledo_parses_explicit_decimal_stable()
    {
        var p = new ToledoProtocol();
        bool ok = p.TryParse(FrameFactory.Delimited("005.250kg"), out var r);

        Assert.True(ok);
        Assert.Equal(WeightStatus.Estavel, r.Status);
        Assert.Equal(5.250m, r.Weight);
    }

    [Fact]
    public void Toledo_parses_implicit_three_decimals()
    {
        var p = new ToledoProtocol();
        bool ok = p.TryParse(FrameFactory.Delimited("001234"), out var r);

        Assert.True(ok);
        Assert.Equal(1.234m, r.Weight);
    }

    [Fact]
    public void Filizola_detects_negative()
    {
        var p = new FilizolaProtocol();
        bool ok = p.TryParse(FrameFactory.Delimited("-01.250"), out var r);

        Assert.True(ok);
        Assert.Equal(WeightStatus.Negativo, r.Status);
        Assert.True(r.Weight < 0m);
    }

    [Fact]
    public void Urano_detects_instability()
    {
        var p = new UranoProtocol();
        bool ok = p.TryParse(FrameFactory.Delimited("I000.000"), out var r);

        Assert.True(ok);
        Assert.Equal(WeightStatus.Instavel, r.Status);
    }

    [Fact]
    public void Generic_parses_weight_with_unit()
    {
        var p = new GenericHeuristicProtocol();
        bool ok = p.TryParse(FrameFactory.Delimited("012.500kg"), out var r);

        Assert.True(ok);
        Assert.Equal(12.500m, r.Weight);
        Assert.Equal("kg", r.Unit);
    }

    [Fact]
    public void Generic_rejects_line_noise_without_frame()
    {
        var p = new GenericHeuristicProtocol();
        // Sem STX e sem ponto decimal: não deve ser confundido com peso.
        byte[] noise = { 0x41, 0x42, 0x43, 0x44 }; // "ABCD"
        bool ok = p.TryParse(noise, out _);

        Assert.False(ok);
    }

    [Fact]
    public void Incomplete_frame_is_not_parsed()
    {
        var p = new ToledoProtocol();
        // STX presente, sem ETX nem CR: frame incompleto.
        byte[] partial = { SerialControl.STX, (byte)'0', (byte)'0', (byte)'5' };
        bool ok = p.TryParse(partial, out _);

        Assert.False(ok);
    }
}
