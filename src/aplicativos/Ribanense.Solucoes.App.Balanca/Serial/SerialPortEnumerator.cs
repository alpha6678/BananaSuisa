using System.IO.Ports;
using System.Management;
using System.Text.RegularExpressions;

namespace Ribanense.Solucoes.App.Balanca.Serial;

/// <summary>
/// Enumera portas seriais físicas e USB-serial presentes na máquina, garantindo
/// portas de COM1 a COM12 como baseline e filtrando portas seriais virtuais Bluetooth.
/// </summary>
public static partial class SerialPortEnumerator
{
    private const int MinBaselineComPort = 1;
    private const int MaxBaselineComPort = 12;

    public static IReadOnlyList<SerialPortInfo> Enumerate()
    {
        string[] detectedNames;
        try { detectedNames = SerialPort.GetPortNames(); }
        catch { detectedNames = Array.Empty<string>(); }

        var (friendlyNames, bluetoothPorts) = InspectWmiDevices();

        // Baseline portas COM1 a COM12 para garantir presença na interface + portas detectadas no sistema.
        var candidatePorts = Enumerable.Range(MinBaselineComPort, MaxBaselineComPort - MinBaselineComPort + 1)
            .Select(i => $"COM{i}")
            .Concat(detectedNames.Select(NormalizePort))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(port => !bluetoothPorts.Contains(port))
            .OrderBy(ComPortOrder)
            .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return candidatePorts
            .Select(p => new SerialPortInfo(p, friendlyNames.GetValueOrDefault(p)))
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

    private static (Dictionary<string, string> FriendlyNames, HashSet<string> BluetoothPorts) InspectWmiDevices()
    {
        var friendlyNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var bluetoothPorts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!OperatingSystem.IsWindows()) return (friendlyNames, bluetoothPorts);

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT PNPDeviceID, Name, Service, Description, Caption FROM Win32_PnPEntity WHERE Name LIKE '%(COM%' OR PNPClass = 'Ports'");

            foreach (ManagementBaseObject device in searcher.Get())
            {
                string? pnpDeviceId = device["PNPDeviceID"]?.ToString();
                string? name = device["Name"]?.ToString();
                string? service = device["Service"]?.ToString();
                string? description = device["Description"]?.ToString();
                string? caption = device["Caption"]?.ToString();

                string searchContext = $"{pnpDeviceId} {name} {service} {description} {caption}";
                var match = ComPortRegex().Match(name ?? caption ?? "");

                if (!match.Success) continue;
                string port = match.Value.ToUpperInvariant();

                if (IsBluetoothDevice(searchContext))
                {
                    bluetoothPorts.Add(port);
                }
                else
                {
                    string label = !string.IsNullOrWhiteSpace(name) ? name : (caption ?? description ?? port);
                    string friendly = FriendlyNameRegex().Replace(label, string.Empty).Trim();
                    friendlyNames[port] = string.IsNullOrWhiteSpace(friendly) ? label.Trim() : friendly;
                }
            }
        }
        catch
        {
            // WMI indisponível: segue sem metadados WMI.
        }

        return (friendlyNames, bluetoothPorts);
    }

    private static bool IsBluetoothDevice(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return text.Contains("bluetooth", StringComparison.OrdinalIgnoreCase)
            || text.Contains("bthenum", StringComparison.OrdinalIgnoreCase)
            || text.Contains("bthmodem", StringComparison.OrdinalIgnoreCase)
            || text.Contains(@"bth\", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"COM(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex ComPortRegex();

    [GeneratedRegex(@"\s*\(COM\d+\)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex FriendlyNameRegex();
}
