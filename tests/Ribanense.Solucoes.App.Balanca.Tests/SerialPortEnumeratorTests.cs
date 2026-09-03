using Ribanense.Solucoes.App.Balanca.Serial;
using Xunit;

namespace Ribanense.Solucoes.App.Balanca.Tests;

public class SerialPortEnumeratorTests
{
    [Fact]
    public void Enumerate_only_returns_present_com_ports()
    {
        // Sem baseline fantasma: só portas realmente presentes. Em máquina sem serial,
        // o resultado é vazio (e não COM1–COM12 inventadas).
        var ports = SerialPortEnumerator.Enumerate(probeOccupancy: false);

        foreach (var info in ports)
        {
            Assert.StartsWith("COM", info.Port, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Classify_recognizes_bluetooth_link_ports()
    {
        // No checkout essas portas costumam ser a maquininha TEF; precisam aparecer na
        // lista (classificadas) em vez de serem escondidas do usuário.
        var kind = SerialPortEnumerator.Classify(
            @"BTHENUM\{00001101-0000-1000-8000-00805F9B34FB}_LOCALMFG&0000",
            "Serial Padrão por link Bluetooth (COM3)");

        Assert.Equal(SerialPortKind.Bluetooth, kind);
    }

    [Theory]
    [InlineData(@"USB\VID_1A86&PID_7523\5&2F1B0A3", "USB-SERIAL CH340 (COM5)")]
    [InlineData(@"FTDIBUS\VID_0403+PID_6001+A1B2C3A\0000", "USB Serial Port (COM7)")]
    public void Classify_recognizes_usb_serial_adapters(string pnpDeviceId, string name)
    {
        Assert.Equal(SerialPortKind.UsbSerial, SerialPortEnumerator.Classify(pnpDeviceId, name));
    }

    [Fact]
    public void Classify_recognizes_native_board_port()
    {
        var kind = SerialPortEnumerator.Classify(@"ACPI\PNP0501\1", "Porta de Comunicação (COM1)");

        Assert.Equal(SerialPortKind.Nativa, kind);
    }

    [Fact]
    public void Classify_falls_back_to_unknown()
    {
        Assert.Equal(SerialPortKind.Desconhecida, SerialPortEnumerator.Classify(null, "COM9"));
    }

    [Fact]
    public void Device_manager_label_matches_windows_format()
    {
        var info = new SerialPortInfo("COM5", "USB-SERIAL CH340", Kind: SerialPortKind.UsbSerial);

        Assert.Equal("USB-SERIAL CH340 (COM5)", info.DeviceManagerLabel);
        Assert.Equal(info.DeviceManagerLabel, info.Display);
    }

    [Fact]
    public void Device_manager_label_falls_back_to_port_without_friendly_name()
    {
        var info = new SerialPortInfo("COM9", null);

        Assert.Equal("COM9", info.DeviceManagerLabel);
    }

    [Fact]
    public void Bluetooth_port_hints_tef_and_usb_serial_hints_scale()
    {
        var tef = new SerialPortInfo("COM3", "Serial Padrão por link Bluetooth", Kind: SerialPortKind.Bluetooth);
        var scale = new SerialPortInfo("COM5", "USB-SERIAL CH340", Kind: SerialPortKind.UsbSerial);

        Assert.True(tef.IsBluetooth);
        Assert.Contains("TEF", tef.RoleHint, StringComparison.OrdinalIgnoreCase);
        Assert.True(scale.IsUsbSerial);
        Assert.Contains("balança", scale.RoleHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Busy_port_reports_in_use_status()
    {
        var free = new SerialPortInfo("COM5", "USB-SERIAL CH340", Kind: SerialPortKind.UsbSerial);
        var busy = free with { IsBusy = true };

        Assert.Equal("Livre", free.StatusHint);
        Assert.Contains("Em uso", busy.StatusHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SerialPortInfo_stable_id_prefers_vid_pid()
    {
        var info = new SerialPortInfo("COM7", "USB Serial", @"USB\VID_0403&PID_6001\ABC", "0403", "6001");
        Assert.Equal("VID_0403&PID_6001", info.StableId);
    }
}
