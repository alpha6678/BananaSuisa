using System.IO.Ports;
using System.Management;
using System.Text.RegularExpressions;

namespace Ribanense.Solucoes.App.Balanca.Serial;

/// <summary>
/// Enumera as portas seriais realmente presentes na máquina (físicas, USB-serial e
/// virtuais de link Bluetooth), sem inventar COM1–COM12, reproduzindo o que o
/// Gerenciador de Dispositivos mostra em "Portas (COM e LPT)".
/// </summary>
/// <remarks>
/// Portas Bluetooth aparecem na lista em vez de serem escondidas: num checkout elas
/// costumam ser a maquininha TEF, e o usuário precisa vê-las para não confundi-las com
/// a balança. Cada porta leva sua origem (<see cref="SerialPortKind"/>) e, quando
/// solicitado, se está ocupada por outro programa.
/// </remarks>
public static partial class SerialPortEnumerator
{
    /// <param name="probeOccupancy">
    /// Quando verdadeiro, tenta abrir cada porta rapidamente para descobrir se já está
    /// em uso por outro programa. A porta é fechada em seguida e nada é transmitido.
    /// Portas de link Bluetooth ficam de fora da sondagem: abrir uma delas dispara a
    /// tentativa de conexão com o dispositivo pareado (a maquininha TEF, em geral) e
    /// pode demorar segundos.
    /// </param>
    public static IReadOnlyList<SerialPortInfo> Enumerate(bool probeOccupancy = true)
    {
        string[] detectedNames;
        try { detectedNames = SerialPort.GetPortNames(); }
        catch { detectedNames = Array.Empty<string>(); }

        var wmi = InspectWmiDevices();

        return detectedNames
            .Select(NormalizePort)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(ComPortOrder)
            .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Select(p => wmi.TryGetValue(p, out var info) ? info : new SerialPortInfo(p, null))
            .Select(info => probeOccupancy && info.Kind != SerialPortKind.Bluetooth
                ? info with { IsBusy = IsPortBusy(info.Port) }
                : info)
            .ToList();
    }

    /// <summary>
    /// Detecta se a porta já está aberta por outro processo. A verificação abre e fecha
    /// a porta sem enviar bytes; falhas diferentes de acesso negado são tratadas como
    /// "não ocupada" para não bloquear o usuário por um diagnóstico incerto.
    /// </summary>
    public static bool IsPortBusy(string port)
    {
        if (string.IsNullOrWhiteSpace(port)) return false;

        try
        {
            using var probe = new SerialPort(port);
            probe.Open();
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Classifica a origem da porta a partir dos textos do WMI (PNP ID, nome, serviço).
    /// </summary>
    public static SerialPortKind Classify(string? pnpDeviceId, string? searchContext)
    {
        string text = $"{pnpDeviceId} {searchContext}";

        if (IsBluetoothDevice(text)) return SerialPortKind.Bluetooth;

        if (pnpDeviceId?.StartsWith(@"USB\", StringComparison.OrdinalIgnoreCase) == true
            || pnpDeviceId?.StartsWith(@"FTDIBUS\", StringComparison.OrdinalIgnoreCase) == true
            || ContainsAny(text, "ftdi", "ch340", "ch341", "cp210", "pl2303", "prolific", "usb-serial", "usb serial"))
        {
            return SerialPortKind.UsbSerial;
        }

        if (pnpDeviceId?.StartsWith(@"ACPI\", StringComparison.OrdinalIgnoreCase) == true
            || pnpDeviceId?.StartsWith(@"PCI\", StringComparison.OrdinalIgnoreCase) == true
            || ContainsAny(text, "communications port", "porta de comunica"))
        {
            return SerialPortKind.Nativa;
        }

        return SerialPortKind.Desconhecida;
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

    private static Dictionary<string, SerialPortInfo> InspectWmiDevices()
    {
        var devices = new Dictionary<string, SerialPortInfo>(StringComparer.OrdinalIgnoreCase);

        if (!OperatingSystem.IsWindows()) return devices;

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

                var match = ComPortRegex().Match(name ?? caption ?? "");
                if (!match.Success) continue;
                string port = match.Value.ToUpperInvariant();

                string label = !string.IsNullOrWhiteSpace(name) ? name : (caption ?? description ?? port);
                string friendly = FriendlyNameRegex().Replace(label, string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(friendly)) friendly = label.Trim();

                var (vid, pid) = ExtractVidPid(pnpDeviceId);
                var kind = Classify(pnpDeviceId, $"{name} {service} {description} {caption}");
                devices[port] = new SerialPortInfo(port, friendly, pnpDeviceId, vid, pid, kind);
            }
        }
        catch
        {
            // WMI indisponível: segue sem metadados WMI.
        }

        return devices;
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

    private static bool IsBluetoothDevice(string text) =>
        ContainsAny(text, "bluetooth", "bthenum", "bthmodem", @"bth\");

    private static bool ContainsAny(string text, params string[] needles)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        foreach (string needle in needles)
        {
            if (text.Contains(needle, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
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
