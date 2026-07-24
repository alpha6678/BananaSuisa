using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Ribanense.Solucoes.App.Balanca.Domain;

namespace Ribanense.Solucoes.App.Balanca.Protocols;

/// <summary>
/// Rotinas compartilhadas de extração de peso a partir de frames seriais.
/// </summary>
public static partial class WeightFrameParser
{
    public static string ToAscii(ReadOnlySpan<byte> buffer)
    {
        var sb = new StringBuilder(buffer.Length);
        foreach (byte b in buffer)
        {
            sb.Append(b is >= 0x20 and < 0x7F ? (char)b : ' ');
        }
        return sb.ToString();
    }

    public static string ToHex(ReadOnlySpan<byte> buffer) =>
        Convert.ToHexString(buffer);

    /// <summary>
    /// Retorna o conteúdo entre STX e ETX. Se não houver delimitadores, devolve
    /// o buffer inteiro. Retorna false quando um STX foi visto mas o ETX ainda não
    /// chegou (frame incompleto).
    /// </summary>
    public static bool TryExtractPayload(ReadOnlySpan<byte> buffer, out ReadOnlySpan<byte> payload)
    {
        payload = default;
        if (buffer.IsEmpty) return false;

        int stx = buffer.IndexOf(SerialControl.STX);
        if (stx < 0)
        {
            // Sem STX: aceita frame terminado por CR/LF ou usa tudo.
            payload = buffer;
            return true;
        }

        var afterStx = buffer[(stx + 1)..];
        int etx = afterStx.IndexOf(SerialControl.ETX);
        if (etx < 0)
        {
            int cr = afterStx.IndexOf(SerialControl.CR);
            if (cr < 0) return false; // frame ainda incompleto
            payload = afterStx[..cr];
            return true;
        }

        payload = afterStx[..etx];
        return true;
    }

    /// <summary>
    /// Detecta marcadores de status ACBr comuns dentro do payload textual.
    /// </summary>
    public static WeightStatus DetectStatus(string ascii)
    {
        string upper = ascii.ToUpperInvariant();
        if (upper.Contains('S') && !upper.Contains("KG")) return WeightStatus.Sobrecarga;
        if (upper.Contains('I')) return WeightStatus.Instavel;
        if (upper.Contains('N') && !upper.Contains("KG")) return WeightStatus.Negativo;
        if (ascii.Contains('-')) return WeightStatus.Negativo;
        return WeightStatus.Estavel;
    }

    /// <summary>
    /// Extrai um número decimal explícito (com ponto/vírgula) do texto, se existir.
    /// </summary>
    public static bool TryParseExplicitDecimal(string ascii, out decimal value)
    {
        value = 0m;
        var m = ExplicitDecimalRegex().Match(ascii);
        if (!m.Success) return false;
        string normalized = m.Value.Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Extrai dígitos e aplica um número fixo de casas decimais implícitas
    /// (ex.: "01234" com 3 casas => 1.234). Usado por balanças sem ponto decimal.
    /// </summary>
    public static bool TryParseImplicit(string ascii, int impliedDecimals, out decimal value)
    {
        value = 0m;
        var m = DigitsRegex().Match(ascii);
        if (!m.Success) return false;
        if (!long.TryParse(m.Value, out long raw)) return false;
        value = impliedDecimals <= 0
            ? raw
            : raw / (decimal)Math.Pow(10, impliedDecimals);
        return true;
    }

    [GeneratedRegex(@"[+-]?\d+[.,]\d+")]
    private static partial Regex ExplicitDecimalRegex();

    [GeneratedRegex(@"\d{2,}")]
    private static partial Regex DigitsRegex();
}
