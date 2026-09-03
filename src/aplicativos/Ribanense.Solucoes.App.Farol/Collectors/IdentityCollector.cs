using System.Runtime.InteropServices;
using Ribanense.Solucoes.App.Farol.Domain;

namespace Ribanense.Solucoes.App.Farol.Collectors;

public sealed class IdentityCollector : ICollector
{
    public string Id => "identity";
    public string DisplayName => "Identidade da máquina";

    public Task CollectAsync(EvidenceBundleBuilder builder, CancellationToken ct)
    {
        builder.Identity = new IdentityInfo(
            HostName: Environment.MachineName,
            UserName: Environment.UserName,
            OsDescription: RuntimeInformation.OSDescription,
            Architecture: RuntimeInformation.OSArchitecture.ToString(),
            UptimeHours: Math.Round(Environment.TickCount64 / 3_600_000.0, 2),
            TimeZone: TimeZoneInfo.Local.Id,
            LocalTime: DateTimeOffset.Now);

        return Task.CompletedTask;
    }
}
