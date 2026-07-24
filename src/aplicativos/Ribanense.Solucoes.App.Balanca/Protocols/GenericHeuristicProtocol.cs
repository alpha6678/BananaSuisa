using System.IO.Ports;
using Ribanense.Solucoes.App.Balanca.Domain;

namespace Ribanense.Solucoes.App.Balanca.Protocols;

/// <summary>
/// Protocolo genérico/automático. Não conhece a marca: solicita o peso com ENQ e
/// reconhece o peso por heurística (número decimal explícito, ou dígitos dentro de
/// um frame STX/ETX). Cobre modelos sem driver dedicado.
/// </summary>
public sealed class GenericHeuristicProtocol : DelimitedWeightProtocol
{
    public override string Key => "generico";
    public override string DisplayName => "Automático / Genérico";

    public override SerialConfig DefaultConfig(string port) =>
        Config(port, 9600, 8, Parity.None, StopBits.One);

    public override bool TryParse(ReadOnlySpan<byte> buffer, out WeightReading reading)
    {
        reading = WeightReading.NotRead();
        if (buffer.IsEmpty) return false;

        if (!WeightFrameParser.TryExtractPayload(buffer, out var payload))
            return false;

        string ascii = WeightFrameParser.ToAscii(payload).Trim();
        if (ascii.Length == 0) return false;

        string rawAscii = WeightFrameParser.ToAscii(buffer).Trim();
        string rawHex = WeightFrameParser.ToHex(buffer);
        string unit = DetectUnit(ascii);
        var status = WeightFrameParser.DetectStatus(ascii);

        bool hasFrame = buffer.IndexOf(SerialControl.STX) >= 0;

        decimal weight;
        if (WeightFrameParser.TryParseExplicitDecimal(ascii, out decimal explicitValue))
        {
            weight = Math.Abs(explicitValue);
        }
        else if (hasFrame && WeightFrameParser.TryParseImplicit(ascii, ImpliedDecimals, out decimal implicitValue))
        {
            // Só aceita dígitos "crus" quando vieram dentro de um frame delimitado,
            // para não confundir ruído de linha com peso.
            weight = implicitValue;
        }
        else
        {
            return false;
        }

        if (status == WeightStatus.Negativo) weight = -Math.Abs(weight);

        reading = new WeightReading(status, weight, unit, rawAscii, rawHex);
        return true;
    }

    private static string DetectUnit(string ascii)
    {
        string lower = ascii.ToLowerInvariant();
        if (lower.Contains("kg")) return "kg";
        if (lower.Contains('g')) return "g";
        return "kg";
    }
}
