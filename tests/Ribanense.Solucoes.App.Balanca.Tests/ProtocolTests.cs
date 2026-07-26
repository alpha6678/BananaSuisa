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
        bool ok = p.TryReadWeight(FrameFactory.Delimited("005.250kg"), out var r);

        Assert.True(ok);
        Assert.Equal(WeightStatus.Estavel, r.Status);
        Assert.Equal(5.250m, r.Weight);
        Assert.True(r.HasWeight);
        Assert.True(r.IsUsable);
    }

    [Fact]
    public void Toledo_parses_implicit_three_decimals()
    {
        var p = new ToledoProtocol();
        bool ok = p.TryReadWeight(FrameFactory.Delimited("001234"), out var r);

        Assert.True(ok);
        Assert.Equal(1.234m, r.Weight);
    }

    [Fact]
    public void Toledo_accepts_stable_zero()
    {
        var p = new ToledoProtocol();
        bool ok = p.TryReadWeight(FrameFactory.Delimited("000000"), out var r);

        Assert.True(ok);
        Assert.Equal(WeightStatus.Estavel, r.Status);
        Assert.Equal(0m, r.Weight);
        Assert.True(r.HasWeight);
        Assert.True(r.IsUsable);
    }

    [Theory]
    [InlineData("IIIII", WeightStatus.Instavel)]
    [InlineData("NNNNN", WeightStatus.Negativo)]
    [InlineData("SSSSS", WeightStatus.Sobrecarga)]
    public void Toledo_recognizes_status_without_weight(string body, WeightStatus expected)
    {
        var p = new ToledoProtocol();
        bool ok = p.TryReadWeight(FrameFactory.Delimited(body), out var r);

        Assert.True(ok);
        Assert.Equal(expected, r.Status);
        Assert.False(r.HasWeight);
        Assert.True(r.HasResponse);
        Assert.False(r.IsUsable);
    }

    [Fact]
    public void Filizola_detects_negative()
    {
        var p = new FilizolaProtocol();
        bool ok = p.TryReadWeight(FrameFactory.Delimited("-01.250"), out var r);

        Assert.True(ok);
        Assert.Equal(WeightStatus.Negativo, r.Status);
        Assert.True(r.Weight < 0m);
    }

    [Fact]
    public void Urano_detects_instability()
    {
        var p = new UranoProtocol();
        bool ok = p.TryReadWeight(FrameFactory.Delimited("I000.000"), out var r);

        Assert.True(ok);
        Assert.Equal(WeightStatus.Instavel, r.Status);
    }

    [Fact]
    public void Urano_parses_text_line_format()
    {
        var p = new UranoProtocol();
        bool ok = p.TryReadWeight(FrameFactory.Line("PESO: 5,10kg"), out var r);

        Assert.True(ok);
        Assert.Equal(WeightStatus.Estavel, r.Status);
        Assert.Equal(5.10m, r.Weight);
        Assert.Equal("kg", r.Unit);
    }

    [Fact]
    public void Urano_default_config_is_8N2()
    {
        var cfg = new UranoProtocol().DefaultConfig("COM3");
        Assert.Equal(System.IO.Ports.StopBits.Two, cfg.StopBits);
    }

    [Fact]
    public void Toledo2180_parses_marker_frame()
    {
        var p = new Toledo2180Protocol();
        bool ok = p.TryReadWeight(FrameFactory.Toledo2180("012500"), out var r);

        Assert.True(ok);
        Assert.Equal(WeightStatus.Estavel, r.Status);
        Assert.Equal(12.500m, r.Weight);
    }

    [Fact]
    public void Toledo2180_ignores_line_without_marker()
    {
        var p = new Toledo2180Protocol();
        bool ok = p.TryReadWeight(FrameFactory.Line("garbage line"), out _);

        Assert.False(ok);
    }

    [Fact]
    public void Generic_parses_weight_with_unit()
    {
        var p = new GenericHeuristicProtocol();
        bool ok = p.TryReadWeight(FrameFactory.Delimited("012.500kg"), out var r);

        Assert.True(ok);
        Assert.Equal(12.500m, r.Weight);
        Assert.Equal("kg", r.Unit);
    }

    [Fact]
    public void Generic_rejects_line_noise_without_frame()
    {
        var p = new GenericHeuristicProtocol();
        byte[] noise = { 0x41, 0x42, 0x43, 0x44 }; // "ABCD"
        bool ok = p.TryReadWeight(noise, out _);

        Assert.False(ok);
    }

    [Fact]
    public void Incomplete_frame_is_not_parsed()
    {
        var p = new ToledoProtocol();
        byte[] partial = { SerialControl.STX, (byte)'0', (byte)'0', (byte)'5' };
        bool ok = p.TryReadWeight(partial, out _);

        Assert.False(ok);
    }

    [Fact]
    public void Fragmented_frame_parses_at_every_chunk_size()
    {
        var p = new ToledoProtocol();
        byte[] frame = FrameFactory.Delimited("005.250");

        for (int chunk = 1; chunk <= frame.Length; chunk++)
        {
            var r = IncrementalFeeder.Feed(p, frame, chunk);
            Assert.NotNull(r);
            Assert.Equal(5.250m, r!.Weight);
        }
    }

    [Fact]
    public void Noise_before_stx_is_resynchronized()
    {
        var p = new ToledoProtocol();
        var noise = new byte[] { 0x00, 0xFF, 0x41 };
        var frame = FrameFactory.Delimited("001.500");
        byte[] data = noise.Concat(frame).ToArray();

        var r = IncrementalFeeder.Feed(p, data, chunkSize: 2);
        Assert.NotNull(r);
        Assert.Equal(1.500m, r!.Weight);
    }

    [Fact]
    public void Concatenated_frames_parse_first_then_second()
    {
        var p = new ToledoProtocol();
        byte[] first = FrameFactory.Delimited("001.000");
        byte[] second = FrameFactory.Delimited("002.000");
        byte[] data = first.Concat(second).ToArray();

        // Consumir o primeiro frame e garantir que o segundo ainda é reconhecível.
        var acc = new System.Collections.Generic.List<byte>(data);
        var r1 = p.Read(acc.ToArray(), isFinal: false);
        Assert.Equal(FrameParseStatus.FrameParsed, r1.Status);
        Assert.Equal(1.000m, r1.Reading!.Weight);

        var rest = acc.Skip(r1.Consumed).ToArray();
        bool ok2 = p.TryReadWeight(rest, out var r2);
        Assert.True(ok2);
        Assert.Equal(2.000m, r2.Weight);
    }

    [Fact]
    public void Delimited_frame_has_high_confidence()
    {
        var p = new ToledoProtocol();
        var result = p.Read(FrameFactory.Delimited("005.250"), isFinal: true);
        Assert.Equal(FrameConfidence.High, result.Confidence);
    }
}
