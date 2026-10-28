using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Ribanense.Solucoes.App.Recuperacao.Recovery.Core;

/// <summary>
/// Executa trechos de PowerShell (leitura via cmdlets como Get-PnpDevice e
/// Get-WindowsDriver) capturando a saída e desserializando JSON — substituto
/// estruturado das chamadas Get-PnpDevice/Get-WindowsDriver do script.
/// </summary>
public sealed class PowerShellProbe(IProcessRunner runner)
{
    public static readonly string PowershellExe = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System), "powershell.exe");

    /// <summary>Monta a linha de comando completa para um script.</summary>
    public static string BuildArguments(string script) =>
        $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{Encode(script)}\"";

    /// <summary>Escapa aspas duplas para uso dentro de -Command "...".</summary>
    public static string Encode(string script) =>
        script.Replace("\"", "\\\"");

    /// <summary>Rodapé padrão que força UTF-8 na saída do console.</summary>
    public static string WithUtf8(string script) =>
        "[Console]::OutputEncoding=[System.Text.Encoding]::UTF8; " + script;

    public async Task<ProcessResult> RunAsync(
        string script, IProgress<string>? output = null, CancellationToken ct = default, int timeoutMinutes = 15)
    {
        var request = new ProcessRequest(PowershellExe, BuildArguments(WithUtf8(script)), timeoutMinutes);
        return await runner.RunAsync(request, output, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Executa um script que emite JSON e devolve uma lista de itens. Lida com a
    /// particularidade do PowerShell 5.1 (objeto único sem array; vazio sem saída).
    /// </summary>
    public async Task<List<T>> QueryJsonListAsync<T>(
        string script, IProgress<string>? output = null, CancellationToken ct = default, int timeoutMinutes = 15)
    {
        ProcessResult result = await RunAsync(script, output, ct, timeoutMinutes).ConfigureAwait(false);
        if (!result.Ok)
            throw new InvalidOperationException(
                $"PowerShell retornou código {result.ExitCode}: {LastLine(result.OutputText)}");

        return ParseJsonList<T>(result.OutputText);
    }

    /// <summary>Executa um script que emite um único objeto JSON e o desserializa.</summary>
    public async Task<T?> QueryJsonObjectAsync<T>(
        string script, IProgress<string>? output = null, CancellationToken ct = default, int timeoutMinutes = 15)
        where T : class
    {
        ProcessResult result = await RunAsync(script, output, ct, timeoutMinutes).ConfigureAwait(false);
        if (!result.Ok)
            throw new InvalidOperationException(
                $"PowerShell retornou código {result.ExitCode}: {LastLine(result.OutputText)}");

        return ParseJsonObject<T>(result.OutputText);
    }

    public static T? ParseJsonObject<T>(string json) where T : class
    {
        string trimmed = json.Trim();
        if (trimmed.Length == 0) return null;

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                ? doc.RootElement.Deserialize<T>(JsonOptions)
                : null;
        }
        catch (JsonException)
        {
            int start = trimmed.IndexOf('{');
            if (start < 0) return null;
            try
            {
                using var doc = JsonDocument.Parse(trimmed[start..]);
                return doc.RootElement.ValueKind == JsonValueKind.Object
                    ? doc.RootElement.Deserialize<T>(JsonOptions)
                    : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    public static List<T> ParseJsonList<T>(string json)
    {
        string trimmed = json.Trim();
        if (trimmed.Length == 0) return [];

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                return doc.RootElement.EnumerateArray()
                    .Select(e => e.Deserialize<T>(JsonOptions))
                    .Where(v => v is not null)
                    .Cast<T>()
                    .ToList();

            T? single = doc.RootElement.Deserialize<T>(JsonOptions);
            return single is null ? [] : [single];
        }
        catch (JsonException)
        {
            // Saída mista (avisos + JSON): tenta recortar do primeiro '{' ou '['.
            int startObj = trimmed.IndexOf('{');
            int startArr = trimmed.IndexOf('[');
            int start = (startObj, startArr) switch
            {
                (-1, -1) => -1,
                (-1, int a) => a,
                (int o, -1) => o,
                (int o, int a) => Math.Min(o, a),
            };
            if (start < 0) return [];
            string tail = trimmed[start..];
            try
            {
                using var doc = JsonDocument.Parse(tail);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    return doc.RootElement.EnumerateArray()
                        .Select(e => e.Deserialize<T>(JsonOptions))
                        .Where(v => v is not null)
                        .Cast<T>()
                        .ToList();
                T? single = doc.RootElement.Deserialize<T>(JsonOptions);
                return single is null ? [] : [single];
            }
            catch (JsonException)
            {
                return [];
            }
        }
    }

    /// <summary>Normaliza datas do PowerShell 5.1 (\u002FDate(epoch)\u002F) para texto legível.</summary>
    public static string? NormalizePsDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        int start = raw.IndexOf("/Date(", StringComparison.OrdinalIgnoreCase);
        if (start < 0) return raw;
        int end = raw.IndexOf(')', start);
        if (end < 0) return raw;
        string digits = raw[(start + "/Date(".Length)..end];
        int plus = digits.IndexOf('+');
        if (plus >= 0) digits = digits[..plus];
        if (!long.TryParse(digits, out long epoch)) return raw;
        try
        {
            var dt = DateTimeOffset.FromUnixTimeMilliseconds(epoch).LocalDateTime;
            return dt.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
        }
        catch
        {
            return raw;
        }
    }

    private static string LastLine(string text)
    {
        string[] lines = text.Split('\n');
        return lines.Length > 0 ? lines[^1].Trim() : string.Empty;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}
