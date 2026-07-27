using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Ribanense.Solucoes.App.Scripts.Scripts.Dns;

public sealed class NetworkDnsDetector : INetworkDnsDetector
{
    public IReadOnlyList<DnsServerCandidate> DetectCurrent()
    {
        var results = new List<DnsServerCandidate>();
        var seen = new HashSet<string>();

        foreach (var ni in GetActiveInterfaces())
        {
            System.Net.NetworkInformation.IPInterfaceProperties props;
            try { props = ni.GetIPProperties(); } catch { continue; }

            foreach (var dns in props.DnsAddresses)
            {
                if (dns.AddressFamily != AddressFamily.InterNetwork) continue;

                string ip = dns.ToString();
                if (!seen.Add(ip)) continue;

                results.Add(new DnsServerCandidate($"DNS atual ({ni.Name})", ip, DnsServerOrigin.RedeAtual));
            }
        }

        return results;
    }

    public IReadOnlyList<string> DetectActiveInterfaceNames()
    {
        try
        {
            return GetActiveInterfaces().Select(ni => ni.Name).Distinct().ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static IEnumerable<NetworkInterface> GetActiveInterfaces()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                .Where(ni => ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Where(ni => ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .ToList();
        }
        catch
        {
            return Enumerable.Empty<NetworkInterface>();
        }
    }
}
