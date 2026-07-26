using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Ribanense.Solucoes.App.Balanca.Domain;

namespace Ribanense.Solucoes.App.Balanca.Protocols;

/// <summary>
/// Rotinas de framing e extração de peso. O foco é ser incremental: localizar um
/// frame completo sem interpretar dígitos parciais, preservando as sobras do buffer
/// e permitindo ressincronizar após ruído de linha.
/// </summary>
public static partial class WeightFrameParser
{
    /// <summary>Situação da localização de um frame dentro do buffer acumulado.</summary>
    public enum LocateStatus
    {
        NeedMoreData,
        Found,
        Invalid,
    }

    /// <summary>
    /// Resultado da localização de um frame. É <c>ref struct</c> para carregar o
    /// <see cref="Payload"/> como fatia do buffer original sem cópia.
    /// </summary>
    public ref struct FrameLocation
    {
        public LocateStatus Status;

        /// <summary>Conteúdo útil do frame (sem delimitadores).</summary>
        public ReadOnlySpan<byte> Payload;

        /// <summary>Quantos bytes consumir da frente do buffer.</summary>
        public int Consumed;

        /// <summary>O frame veio delimitado por STX (confiança maior).</summary>
        public bool Delimited;
    }

    public static string ToAscii(ReadOnlySpan<byte> buffer)
    {
        var sb = new StringBuilder(buffer.Length);
        foreach (byte b in buffer)
        {
            sb.Append(b is >= 0x20 and < 0x7F ? (char)b : ' ');
        }
        return sb.ToString();
    }

    public static string ToHex(ReadOnlySpan<byte> buffer) => Convert.ToHexString(buffer);

    /// <summary>
    /// Localiza um frame delimitado por STX e terminado por ETX (ou, na ausência de ETX,
    /// por CR). Ruído antes do STX é descartado para ressincronizar. Enquanto o frame
    /// não estiver completo devolve <see cref="LocateStatus.NeedMoreData"/>.
    /// </summary>
    public static FrameLocation LocateStxFrame(ReadOnlySpan<byte> buffer, bool isFinal)
    {
        if (buffer.IsEmpty)
            return new FrameLocation { Status = isFinal ? LocateStatus.Invalid : LocateStatus.NeedMoreData, Consumed = 0 };

        int stx = buffer.IndexOf(SerialControl.STX);
        if (stx < 0)
        {
            // Sem STX: nada de frame ainda. Em final, descarta tudo como ruído.
            return new FrameLocation
            {
                Status = isFinal ? LocateStatus.Invalid : LocateStatus.NeedMoreData,
                Consumed = isFinal ? buffer.Length : 0,
            };
        }

        var afterStx = buffer[(stx + 1)..];

        int etx = afterStx.IndexOf(SerialControl.ETX);
        if (etx >= 0)
        {
            int end = stx + 1 + etx + 1;
            end += CountTrailingEol(buffer, end);
            return new FrameLocation
            {
                Status = LocateStatus.Found,
                Payload = afterStx[..etx],
                Consumed = end,
                Delimited = true,
            };
        }

        int cr = afterStx.IndexOf(SerialControl.CR);
        if (cr >= 0)
        {
            int end = stx + 1 + cr + 1;
            end += CountTrailingEol(buffer, end);
            return new FrameLocation
            {
                Status = LocateStatus.Found,
                Payload = afterStx[..cr],
                Consumed = end,
                Delimited = true,
            };
        }

        // STX presente mas ainda sem terminador: frame incompleto.
        // Fora do final, descarta apenas o ruído anterior ao STX (ressincroniza) e aguarda.
        return new FrameLocation
        {
            Status = isFinal ? LocateStatus.Invalid : LocateStatus.NeedMoreData,
            Consumed = isFinal ? buffer.Length : stx,
        };
    }

    /// <summary>
    /// Localiza uma linha terminada por CR ou LF (protocolos sem STX). Linhas vazias
    /// são consumidas e sinalizam necessidade de mais dados. No final, aceita o que
    /// houver como melhor esforço.
    /// </summary>
    public static FrameLocation LocateLineFrame(ReadOnlySpan<byte> buffer, bool isFinal)
    {
        if (buffer.IsEmpty)
            return new FrameLocation { Status = isFinal ? LocateStatus.Invalid : LocateStatus.NeedMoreData, Consumed = 0 };

        int term = IndexOfEol(buffer);
        if (term >= 0)
        {
            var payload = buffer[..term];
            int end = term + CountTrailingEol(buffer, term);
            if (IsBlank(payload))
            {
                // Linha em branco (só EOL): descarta e continua procurando.
                return new FrameLocation { Status = LocateStatus.Invalid, Consumed = end };
            }
            return new FrameLocation
            {
                Status = LocateStatus.Found,
                Payload = payload,
                Consumed = end,
                Delimited = false,
            };
        }

        // Sem terminador: aguarda; no final, aceita o buffer como melhor esforço.
        if (isFinal && !IsBlank(buffer))
        {
            return new FrameLocation
            {
                Status = LocateStatus.Found,
                Payload = buffer,
                Consumed = buffer.Length,
                Delimited = false,
            };
        }

        return new FrameLocation
        {
            Status = isFinal ? LocateStatus.Invalid : LocateStatus.NeedMoreData,
            Consumed = isFinal ? buffer.Length : 0,
        };
    }

    /// <summary>
    /// Detecta o status a partir do primeiro caractere significativo do payload,
    /// seguindo a convenção ACBr/Toledo (I/N/S) e o sinal negativo. Não usa "contém"
    /// para evitar falsos positivos com texto.
    /// </summary>
    public static WeightStatus DetectFrameStatus(string payload)
    {
        string t = payload.Trim();
        if (t.Length == 0) return WeightStatus.NaoLido;

        char first = char.ToUpperInvariant(t[0]);
        return first switch
        {
            'I' => WeightStatus.Instavel,
            'N' => WeightStatus.Negativo,
            'S' => WeightStatus.Sobrecarga,
            '-' => WeightStatus.Negativo,
            _ => t.Contains('-') ? WeightStatus.Negativo : WeightStatus.Estavel,
        };
    }

    /// <summary>Detecta a unidade a partir do texto (kg/g), com padrão configurável.</summary>
    public static string DetectUnit(string ascii, string fallback = "kg")
    {
        string lower = ascii.ToLowerInvariant();
        if (lower.Contains("kg")) return "kg";
        if (lower.Contains('g')) return "g";
        return fallback;
    }

    /// <summary>Extrai um número decimal explícito (com ponto/vírgula) do texto, se existir.</summary>
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
    /// Aceita a partir de 1 dígito.
    /// </summary>
    public static bool TryParseImplicit(string ascii, int impliedDecimals, out decimal value)
    {
        value = 0m;
        var m = DigitsRegex().Match(ascii);
        if (!m.Success) return false;
        if (!long.TryParse(m.Value, NumberStyles.None, CultureInfo.InvariantCulture, out long raw)) return false;
        value = impliedDecimals <= 0
            ? raw
            : raw / (decimal)Math.Pow(10, impliedDecimals);
        return true;
    }

    private static int CountTrailingEol(ReadOnlySpan<byte> buffer, int start)
    {
        int count = 0;
        for (int i = start; i < buffer.Length; i++)
        {
            if (buffer[i] is SerialControl.CR or SerialControl.LF) count++;
            else break;
        }
        return count;
    }

    private static int IndexOfEol(ReadOnlySpan<byte> buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            if (buffer[i] is SerialControl.CR or SerialControl.LF) return i;
        }
        return -1;
    }

    private static bool IsBlank(ReadOnlySpan<byte> buffer)
    {
        foreach (byte b in buffer)
        {
            if (b is not (SerialControl.CR or SerialControl.LF or 0x00 or 0x20)) return false;
        }
        return true;
    }

    [GeneratedRegex(@"[+-]?\d+[.,]\d+")]
    private static partial Regex ExplicitDecimalRegex();

    [GeneratedRegex(@"\d+")]
    private static partial Regex DigitsRegex();
}
