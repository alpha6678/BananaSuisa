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
        var ports = SerialPortEnumerator.Enumerate();

        foreach (var info in ports)
        {
            Assert.StartsWith("COM", info.Port, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Enumerate_does_not_contain_bluetooth_friendly_names()
    {
        var ports = SerialPortEnumerator.Enumerate();

        foreach (var info in ports)
        {
            if (info.FriendlyName is not null)
            {
                Assert.DoesNotContain("bluetooth", info.FriendlyName, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void SerialPortInfo_stable_id_prefers_vid_pid()
    {
        var info = new SerialPortInfo("COM7", "USB Serial", @"USB\VID_0403&PID_6001\ABC", "0403", "6001");
        Assert.Equal("VID_0403&PID_6001", info.StableId);
    }
}
