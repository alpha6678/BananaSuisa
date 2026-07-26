using System.IO.Ports;
using System.Management;
using System.Text.RegularExpressions;

namespace Ribanense.Solucoes.App.Balanca.Serial;

/// <summary>
/// Enumera apenas as portas seriais realmente presentes na máquina (físicas e
/// USB-serial), sem inventar COM1–COM12. Filtra portas seriais virtuais Bluetooth e
/// anexa identidade estável (PNP ID, VID/PID) para reconhecer o mesmo dispositivo após
/// reconexão, quando o número da COM pode mudar.
/// </summary>
public static partial class SerialPortEnumerator
{
    public static IReadOnlyList<SerialPortInfo> Enumerate()
    {
        string[] detectedNames;
        try { detectedNames = SerialPort.GetPortNames(); }
        catch { detectedNames = Array.Empty<string>(); }

        var wmi = InspectWmiDevices();

        return detectedNames
            .Select(NormalizePort)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(port => !wmi.BluetoothPorts.Contains(port))
            .OrderBy(ComPortOrder)
            .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Select(p => wmi.Devices.TryGetValue(p, out var info)
                ? info
                : new SerialPortInfo(p, null))
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

    private static (Dictionary<string, SerialPortInfo> Devices, HashSet<string> BluetoothPorts) InspectWmiDevices()
    {
        var devices = new Dictionary<string, SerialPortInfo>(StringComparer.OrdinalIgnoreCase);
        var bluetoothPorts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!OperatingSystem.IsWindows()) return (devices, bluetoothPorts);

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
                    continue;
                }

                string label = !string.IsNullOrWhiteSpace(name) ? name : (caption ?? description ?? port);
                string friendly = FriendlyNameRegex().Replace(label, string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(friendly)) friendly = label.Trim();

                var (vid, pid) = ExtractVidPid(pnpDeviceId);
                devices[port] = new SerialPortInfo(port, friendly, pnpDeviceId, vid, pid);
            }
        }
        catch
        {
            // WMI indisponível: segue sem metadados WMI.
        }

        return (devices, bluetoothPorts);
    }

    private static (string? Vid, string? Pid) ExtractVidPid(string? pnpDeviceId)
    {
        if (string.IsNullOrWhiteSpace(pnpDeviceId)) return (null, null);
        var vid = VidRegex().Match(pnpDeviceId);
        var pid = PidRegex().Match(pnpDeviceId);
        return (
            vid.Success ? vid.Groups[1].Value.ToUpperInvariant() : null,
            pid.Success ? pid.Groups[1].Value.ToUpperInvariant() : null);
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

    [GeneratedRegex(@"VID_([0-9A-Fa-f]{4})", RegexOptions.IgnoreCase)]
    private static partial Regex VidRegex();

    [GeneratedRegex(@"PID_([0-9A-Fa-f]{4})", RegexOptions.IgnoreCase)]
    private static partial Regex PidRegex();
}
