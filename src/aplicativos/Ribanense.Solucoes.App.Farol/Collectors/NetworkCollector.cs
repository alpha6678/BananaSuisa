using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Ribanense.Solucoes.App.Farol.Domain;

namespace Ribanense.Solucoes.App.Farol.Collectors;

public sealed class NetworkCollector : ICollector
{
    // CLSID do NetworkListManager. Usado por late binding para não precisar de
    // assembly de interop só para descobrir se a rede é Pública ou Privada.
    private static readonly Guid NetworkListManagerClsid =
        new("DCB00C01-570F-4A9B-8D69-199FDBA5723B");

    private const int NlmEnumNetworkConnected = 1;

    public string Id => "network";
    public string DisplayName => "Rede";

    public async Task CollectAsync(EvidenceBundleBuilder builder, CancellationToken ct)
    {
        var adapters = new List<AdapterInfo>();
        var dnsServers = new List<string>();
        string? primaryGateway = null;

        foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            ct.ThrowIfCancellationRequested();

            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            IPInterfaceProperties props = nic.GetIPProperties();

            var ipV4 = props.UnicastAddresses
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.Address.ToString())
                .ToArray();

            var gateways = props.GatewayAddresses
                .Where(g => g.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(g => g.Address.ToString())
                .ToArray();

            var nicDns = props.DnsAddresses
                .Where(d => d.AddressFamily == AddressFamily.InterNetwork)
                .Select(d => d.ToString())
                .ToArray();

            adapters.Add(new AdapterInfo(
                Name: nic.Name,
                Description: nic.Description,
                Status: nic.OperationalStatus.ToString(),
                MacAddress: FormatMac(nic.GetPhysicalAddress()),
                IpV4: ipV4,
                Gateways: gateways,
                DnsServers: nicDns));

            if (nic.OperationalStatus != OperationalStatus.Up) continue;

            dnsServers.AddRange(nicDns);
            primaryGateway ??= gateways.FirstOrDefault();
        }

        long? pingMs = primaryGateway is null
            ? null
            : await PingAsync(primaryGateway, ct).ConfigureAwait(false);

        (NetworkCategory category, string? source) = DetectCategory();

        builder.Network = new NetworkInfo(
            Category: category,
            CategorySource: source,
            Adapters: adapters,
            ActiveDnsServers: dnsServers.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            PrimaryGateway: primaryGateway,
            GatewayPingMs: pingMs);
    }

    private static string FormatMac(PhysicalAddress address)
    {
        byte[] bytes = address.GetAddressBytes();
        return bytes.Length == 0 ? string.Empty : string.Join(":", bytes.Select(b => b.ToString("X2")));
    }

    private static async Task<long?> PingAsync(string host, CancellationToken ct)
    {
        try
        {
            using var ping = new Ping();
            PingReply reply = await ping.SendPingAsync(IPAddress.Parse(host), TimeSpan.FromSeconds(2), cancellationToken: ct)
                .ConfigureAwait(false);
            return reply.Status == IPStatus.Success ? reply.RoundtripTime : null;
        }
        catch (Exception ex) when (ex is PingException or SocketException or FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Lê a categoria da rede conectada. Se qualquer coisa falhar devolve
    /// <see cref="NetworkCategory.Unknown"/>: saber o perfil é útil, não essencial.
    /// </summary>
    private static (NetworkCategory Category, string? Source) DetectCategory()
    {
        if (!OperatingSystem.IsWindows()) return (NetworkCategory.Unknown, null);

        object? manager = null;
        try
        {
            Type? type = Type.GetTypeFromCLSID(NetworkListManagerClsid);
            if (type is null) return (NetworkCategory.Unknown, null);

            manager = Activator.CreateInstance(type);
            if (manager is null) return (NetworkCategory.Unknown, null);

            dynamic nlm = manager;
            NetworkCategory best = NetworkCategory.Unknown;
            string? name = null;

            foreach (dynamic network in nlm.GetNetworks(NlmEnumNetworkConnected))
            {
                NetworkCategory category = ToCategory((int)network.GetCategory());
                if (category > best || best == NetworkCategory.Unknown)
                {
                    best = category;
                    name = (string)network.GetName();
                }
            }

            return (best, name);
        }
        catch
        {
            return (NetworkCategory.Unknown, null);
        }
        finally
        {
            if (manager is not null && Marshal.IsComObject(manager))
            {
                Marshal.FinalReleaseComObject(manager);
            }
        }
    }

    private static NetworkCategory ToCategory(int nlmCategory) => nlmCategory switch
    {
        0 => NetworkCategory.Public,
        1 => NetworkCategory.Private,
        2 => NetworkCategory.Domain,
        _ => NetworkCategory.Unknown,
    };
}
