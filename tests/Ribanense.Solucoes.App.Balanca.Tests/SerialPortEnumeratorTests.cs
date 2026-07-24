using Ribanense.Solucoes.App.Balanca.Serial;
using Xunit;

namespace Ribanense.Solucoes.App.Balanca.Tests;

public class SerialPortEnumeratorTests
{
    [Fact]
    public void Enumerate_contains_baseline_com_ports_excluding_bluetooth()
    {
        var ports = SerialPortEnumerator.Enumerate();

        Assert.NotEmpty(ports);

        foreach (var info in ports)
        {
            Assert.StartsWith("COM", info.Port, StringComparison.OrdinalIgnoreCase);
            if (info.FriendlyName is not null)
            {
                Assert.DoesNotContain("bluetooth", info.FriendlyName, StringComparison.OrdinalIgnoreCase);
            }
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
}
