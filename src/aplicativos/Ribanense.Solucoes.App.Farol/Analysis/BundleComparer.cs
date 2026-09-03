using System.Globalization;
using Ribanense.Solucoes.App.Farol.Domain;

namespace Ribanense.Solucoes.App.Farol.Analysis;

/// <summary>Uma diferença entre esta máquina e a máquina de referência.</summary>
public sealed record BundleDifference(
    string Category,
    string Aspect,
    string Local,
    string Remote,
    FindingSeverity Severity,
    string Note);

/// <summary>
/// Diff estruturado entre dois dossiês. É a pergunta que o suporte sempre faz:
/// "o que essa máquina tem de diferente da que está funcionando?".
/// </summary>
public sealed class BundleComparer
{
    public IReadOnlyList<BundleDifference> Compare(EvidenceBundle local, EvidenceBundle remote)
    {
        ArgumentNullException.ThrowIfNull(local);
        ArgumentNullException.ThrowIfNull(remote);

        var differences = new List<BundleDifference>();

        CompareNetwork(local, remote, differences);
        CompareServices(local, remote, differences);
        ComparePrinters(local, remote, differences);
        CompareDisks(local, remote, differences);
        CompareSystem(local, remote, differences);
        CompareRibanenseApps(local, remote, differences);

        return differences
            .OrderByDescending(d => d.Severity)
            .ThenBy(d => d.Category, StringComparer.CurrentCulture)
            .ToArray();
    }

    private static void CompareNetwork(EvidenceBundle local, EvidenceBundle remote, List<BundleDifference> output)
    {
        NetworkInfo? a = local.Network;
        NetworkInfo? b = remote.Network;
        if (a is null || b is null) return;

        if (a.Category != b.Category)
        {
            output.Add(new BundleDifference(
                "Rede", "Perfil de rede",
                a.Category.ToString(), b.Category.ToString(),
                a.Category == NetworkCategory.Public ? FindingSeverity.High : FindingSeverity.Medium,
                "Perfis diferentes mudam firewall e descoberta. A máquina em Pública fica isolada das outras."));
        }

        string localDns = Join(a.ActiveDnsServers);
        string remoteDns = Join(b.ActiveDnsServers);
        if (!string.Equals(localDns, remoteDns, StringComparison.OrdinalIgnoreCase))
        {
            output.Add(new BundleDifference(
                "Rede", "Servidores DNS",
                localDns, remoteDns,
                FindingSeverity.Medium,
                "DNS divergente entre máquinas da mesma rede costuma ser alteração manual em uma delas."));
        }

        if (!string.Equals(a.PrimaryGateway, b.PrimaryGateway, StringComparison.OrdinalIgnoreCase))
        {
            output.Add(new BundleDifference(
                "Rede", "Gateway",
                a.PrimaryGateway ?? "(nenhum)", b.PrimaryGateway ?? "(nenhum)",
                FindingSeverity.Medium,
                "Gateways diferentes indicam sub-redes ou VLANs distintas."));
        }
    }

    private static void CompareServices(EvidenceBundle local, EvidenceBundle remote, List<BundleDifference> output)
    {
        foreach (ServiceInfo remoteService in remote.Services)
        {
            ServiceInfo? localService = local.FindService(remoteService.Name);
            if (localService is null) continue;

            if (string.Equals(localService.Status, remoteService.Status, StringComparison.OrdinalIgnoreCase))
                continue;

            bool localBroken = !FindingEngine.IsRunning(localService) && FindingEngine.IsRunning(remoteService);

            output.Add(new BundleDifference(
                "Serviços", localService.DisplayName,
                localService.Status, remoteService.Status,
                localBroken ? FindingSeverity.High : FindingSeverity.Low,
                localBroken
                    ? "Está parado só aqui. Forte candidato à causa do problema."
                    : "Diferença de estado; pode ser apenas serviço sob demanda."));
        }
    }

    private static void ComparePrinters(EvidenceBundle local, EvidenceBundle remote, List<BundleDifference> output)
    {
        var localNames = local.Printers.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var remoteNames = remote.Printers.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (string missing in remoteNames.Except(localNames, StringComparer.OrdinalIgnoreCase))
        {
            output.Add(new BundleDifference(
                "Impressão", $"Impressora {missing}",
                "ausente", "instalada",
                FindingSeverity.High,
                "A outra máquina tem essa fila e esta não. Provável fila removida ou nunca instalada aqui."));
        }

        foreach (string extra in localNames.Except(remoteNames, StringComparer.OrdinalIgnoreCase))
        {
            output.Add(new BundleDifference(
                "Impressão", $"Impressora {extra}",
                "instalada", "ausente",
                FindingSeverity.Info,
                "Existe só nesta máquina. Pode ser fila local legítima ou sobra de instalação antiga."));
        }

        foreach (PrinterInfo localPrinter in local.Printers)
        {
            PrinterInfo? remotePrinter = remote.Printers
                .FirstOrDefault(p => string.Equals(p.Name, localPrinter.Name, StringComparison.OrdinalIgnoreCase));

            if (remotePrinter is null || localPrinter.IsOffline == remotePrinter.IsOffline) continue;

            output.Add(new BundleDifference(
                "Impressão", $"Estado de {localPrinter.Name}",
                localPrinter.IsOffline ? "offline" : "online",
                remotePrinter.IsOffline ? "offline" : "online",
                localPrinter.IsOffline ? FindingSeverity.High : FindingSeverity.Low,
                "Mesma fila com estados diferentes nas duas máquinas."));
        }
    }

    private static void CompareDisks(EvidenceBundle local, EvidenceBundle remote, List<BundleDifference> output)
    {
        foreach (DiskInfo localDisk in local.Disks)
        {
            DiskInfo? remoteDisk = remote.Disks
                .FirstOrDefault(d => string.Equals(d.Name, localDisk.Name, StringComparison.OrdinalIgnoreCase));

            if (remoteDisk is null) continue;
            if (localDisk.FreePercent >= FindingEngine.DiskWarningPercent) continue;
            if (remoteDisk.FreePercent < FindingEngine.DiskWarningPercent) continue;

            output.Add(new BundleDifference(
                "Disco", $"Espaço livre em {localDisk.Name}",
                Percent(localDisk.FreePercent), Percent(remoteDisk.FreePercent),
                FindingSeverity.Medium,
                "Só esta máquina está com o disco apertado."));
        }
    }

    private static void CompareSystem(EvidenceBundle local, EvidenceBundle remote, List<BundleDifference> output)
    {
        if (local.Identity is null || remote.Identity is null) return;

        if (!string.Equals(local.Identity.OsDescription, remote.Identity.OsDescription, StringComparison.OrdinalIgnoreCase))
        {
            output.Add(new BundleDifference(
                "Sistema", "Versão do Windows",
                local.Identity.OsDescription, remote.Identity.OsDescription,
                FindingSeverity.Info,
                "Builds diferentes explicam comportamento diferente em impressão e rede."));
        }

        if (!string.Equals(local.FarolVersion, remote.FarolVersion, StringComparison.OrdinalIgnoreCase))
        {
            output.Add(new BundleDifference(
                "Sistema", "Versão do Farol",
                local.FarolVersion, remote.FarolVersion,
                FindingSeverity.Info,
                "Versões diferentes podem coletar seções diferentes."));
        }
    }

    private static void CompareRibanenseApps(EvidenceBundle local, EvidenceBundle remote, List<BundleDifference> output)
    {
        foreach (RibanenseAppInfo remoteApp in remote.RibanenseApps)
        {
            RibanenseAppInfo? localApp = local.RibanenseApps
                .FirstOrDefault(a => string.Equals(a.AppId, remoteApp.AppId, StringComparison.OrdinalIgnoreCase));

            if (localApp is null)
            {
                output.Add(new BundleDifference(
                    "Apps Ribanense", remoteApp.AppId,
                    "não instalado", remoteApp.Version ?? "instalado",
                    FindingSeverity.Info,
                    "App presente só na outra máquina."));
                continue;
            }

            if (!string.Equals(localApp.Version, remoteApp.Version, StringComparison.OrdinalIgnoreCase))
            {
                output.Add(new BundleDifference(
                    "Apps Ribanense", $"Versão de {remoteApp.AppId}",
                    localApp.Version ?? "desconhecida", remoteApp.Version ?? "desconhecida",
                    FindingSeverity.Low,
                    "Versões diferentes do mesmo app entre máquinas."));
            }
        }
    }

    private static string Join(IReadOnlyList<string> values) =>
        values.Count == 0 ? "(nenhum)" : string.Join(", ", values);

    private static string Percent(double value) =>
        value.ToString("0.#", CultureInfo.CurrentCulture) + "%";
}
