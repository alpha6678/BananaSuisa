using System.IO.Ports;
using System.Management;
using System.Text.RegularExpressions;

namespace Ribanense.Solucoes.App.Balanca.Serial;

/// <summary>
/// Enumera portas seriais presentes na máquina, tentando obter nomes amigáveis
/// (adaptadores USB-serial, chips FTDI/CH340, etc.) via WMI. Faz fallback para
/// <see cref="SerialPort.GetPortNames"/> quando o WMI não está disponível.
/// </summary>
public static partial class SerialPortEnumerator
{
    public static IReadOnlyList<SerialPortInfo> Enumerate()
    {
        string[] names;
        try { names = SerialPort.GetPortNames(); }
        catch { names = Array.Empty<string>(); }

        var friendly = TryGetFriendlyNames();

        return names
            .Select(NormalizePort)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(ComPortOrder)
            .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Select(p => new SerialPortInfo(p, friendly.GetValueOrDefault(p)))
            .ToList();
    }

    private static string NormalizePort(string raw)
    {
        // GetPortNames pode devolver sufixos estranhos em alguns drivers; mantém "COMx".
        var match = ComPortRegex().Match(raw);
        return match.Success ? match.Value.ToUpperInvariant() : raw.Trim();
    }

    private static int ComPortOrder(string port)
    {
        var match = ComPortRegex().Match(port);
        return match.Success && int.TryParse(match.Groups[1].Value, out int n) ? n : int.MaxValue;
    }

    private static Dictionary<string, string> TryGetFriendlyNames()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!OperatingSystem.IsWindows()) return result;

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'");
            foreach (ManagementBaseObject device in searcher.Get())
            {
                string? name = device["Name"]?.ToString();
                if (string.IsNullOrWhiteSpace(name)) continue;

                var match = ComPortRegex().Match(name);
                if (!match.Success) continue;

                string port = match.Value.ToUpperInvariant();
                string friendly = FriendlyNameRegex().Replace(name, string.Empty).Trim();
                result[port] = string.IsNullOrWhiteSpace(friendly) ? name.Trim() : friendly;
            }
        }
        catch
        {
            // WMI indisponível: segue só com os nomes das portas.
        }

        return result;
    }

    [GeneratedRegex(@"COM(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex ComPortRegex();

    [GeneratedRegex(@"\s*\(COM\d+\)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex FriendlyNameRegex();
}
