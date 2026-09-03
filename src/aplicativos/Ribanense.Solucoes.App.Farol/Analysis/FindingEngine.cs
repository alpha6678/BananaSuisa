using System.Globalization;
using Ribanense.Solucoes.App.Farol.Domain;

namespace Ribanense.Solucoes.App.Farol.Analysis;

/// <summary>
/// Regras determinísticas sobre o dossiê. Nenhuma heurística estatística e
/// nenhum modelo: toda conclusão precisa apontar a evidência que a sustenta.
/// </summary>
public sealed class FindingEngine
{
    public const double DiskCriticalPercent = 10.0;
    public const double DiskWarningPercent = 20.0;
    public const int EventErrorBurstThreshold = 10;

    public IReadOnlyList<Finding> Evaluate(EvidenceBundle bundle, IReadOnlyList<PeerBeacon>? peers = null)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        var findings = new List<Finding>();

        EvaluateCollectors(bundle, findings);
        EvaluateServices(bundle, findings);
        EvaluateDisks(bundle, findings);
        EvaluateNetwork(bundle, peers, findings);
        EvaluatePrinters(bundle, findings);
        EvaluateEvents(bundle, findings);
        EvaluateRibanenseApps(bundle, findings);

        return findings
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.Category, StringComparer.CurrentCulture)
            .ToArray();
    }

    private static void EvaluateCollectors(EvidenceBundle bundle, List<Finding> findings)
    {
        var blocked = bundle.Collectors
            .Where(c => c.Status is CollectorStatus.Denied or CollectorStatus.Failed)
            .ToArray();

        if (blocked.Length == 0) return;

        findings.Add(new Finding(
            RuleId: "collector.incomplete",
            Severity: FindingSeverity.Info,
            Category: "Coleta",
            Title: $"{blocked.Length} sensor(es) não puderam coletar",
            Evidence: string.Join("; ", blocked.Select(c => $"{c.DisplayName}: {c.Detail ?? c.Status.ToString()}")),
            Suggestion: "O dossiê está incompleto nessas seções. Rodar o Farol como administrador amplia a coleta."));
    }

    private static void EvaluateServices(EvidenceBundle bundle, List<Finding> findings)
    {
        ServiceInfo? spooler = bundle.FindService("Spooler");
        if (spooler is not null && !IsRunning(spooler))
        {
            findings.Add(new Finding(
                RuleId: "service.spooler.stopped",
                Severity: FindingSeverity.High,
                Category: "Impressão",
                Title: "Fila de impressão parada",
                Evidence: $"Serviço Spooler está {spooler.Status} (inicialização {spooler.StartType}).",
                Suggestion: "Nenhuma impressão sai desta máquina. Iniciar o serviço Spooler resolve o sintoma; se ele parar de novo, verificar os eventos do Windows."));
        }

        ServiceInfo? dnsCache = bundle.FindService("Dnscache");
        if (dnsCache is not null && !IsRunning(dnsCache))
        {
            findings.Add(new Finding(
                RuleId: "service.dnscache.stopped",
                Severity: FindingSeverity.Medium,
                Category: "Rede",
                Title: "Cache DNS parado",
                Evidence: $"Serviço Dnscache está {dnsCache.Status}.",
                Suggestion: "Resolução de nomes fica lenta ou intermitente. Iniciar o serviço Dnscache."));
        }

        ServiceInfo? wmi = bundle.FindService("Winmgmt");
        if (wmi is not null && !IsRunning(wmi))
        {
            findings.Add(new Finding(
                RuleId: "service.wmi.stopped",
                Severity: FindingSeverity.Medium,
                Category: "Sistema",
                Title: "WMI parado",
                Evidence: $"Serviço Winmgmt está {wmi.Status}.",
                Suggestion: "Vários diagnósticos do próprio Farol dependem de WMI. Iniciar o serviço Winmgmt."));
        }
    }

    private static void EvaluateDisks(EvidenceBundle bundle, List<Finding> findings)
    {
        foreach (DiskInfo disk in bundle.Disks)
        {
            if (disk.FreePercent >= DiskWarningPercent) continue;

            bool critical = disk.FreePercent < DiskCriticalPercent;

            findings.Add(new Finding(
                RuleId: critical ? "disk.critical" : "disk.low",
                Severity: critical ? FindingSeverity.High : FindingSeverity.Medium,
                Category: "Disco",
                Title: $"Espaço baixo em {disk.Name}",
                Evidence: $"{Format(disk.FreeBytes)} livres de {Format(disk.TotalBytes)} ({disk.FreePercent.ToString("0.#", CultureInfo.CurrentCulture)}%).",
                Suggestion: critical
                    ? "Abaixo de 10% o Windows começa a falhar em atualizações, spool de impressão e gravação de logs. Liberar espaço é urgente."
                    : "Convém liberar espaço antes que chegue no limite crítico."));
        }
    }

    private static void EvaluateNetwork(
        EvidenceBundle bundle,
        IReadOnlyList<PeerBeacon>? peers,
        List<Finding> findings)
    {
        NetworkInfo? network = bundle.Network;
        if (network is null) return;

        if (network.Category == NetworkCategory.Public)
        {
            bool alone = peers is null || peers.Count == 0;

            findings.Add(new Finding(
                RuleId: "network.public-profile",
                Severity: alone ? FindingSeverity.High : FindingSeverity.Medium,
                Category: "Rede",
                Title: "Rede classificada como Pública",
                Evidence: $"Perfil atual: Pública{(network.CategorySource is null ? "" : $" ({network.CategorySource})")}."
                    + (alone ? " Nenhum outro farol foi encontrado." : string.Empty),
                Suggestion: "Em rede Pública o Windows bloqueia descoberta e conexões de entrada. Mudar o perfil para Privada faz os faróis se enxergarem."));
        }

        if (network.PrimaryGateway is null)
        {
            findings.Add(new Finding(
                RuleId: "network.no-gateway",
                Severity: FindingSeverity.High,
                Category: "Rede",
                Title: "Sem gateway padrão",
                Evidence: "Nenhum adaptador ativo declarou gateway IPv4.",
                Suggestion: "A máquina não alcança nada fora da própria sub-rede. Verificar cabo, Wi-Fi e configuração de IP."));
        }
        else if (network.GatewayPingMs is null)
        {
            findings.Add(new Finding(
                RuleId: "network.gateway-unreachable",
                Severity: FindingSeverity.High,
                Category: "Rede",
                Title: "Gateway não responde",
                Evidence: $"Sem resposta de ping do gateway {network.PrimaryGateway}.",
                Suggestion: "Pode ser bloqueio de ICMP no roteador ou queda real do enlace. Confirmar com o acesso à internet."));
        }

        if (network.ActiveDnsServers.Count == 0)
        {
            findings.Add(new Finding(
                RuleId: "network.no-dns",
                Severity: FindingSeverity.High,
                Category: "Rede",
                Title: "Nenhum servidor DNS configurado",
                Evidence: "Os adaptadores ativos não têm DNS IPv4.",
                Suggestion: "Nada resolve por nome. O app Scripts tem o teste de DNS para escolher um servidor rápido."));
        }
    }

    private static void EvaluatePrinters(EvidenceBundle bundle, List<Finding> findings)
    {
        foreach (PrinterInfo printer in bundle.Printers.Where(p => p.IsOffline))
        {
            findings.Add(new Finding(
                RuleId: "printer.offline",
                Severity: printer.IsDefault ? FindingSeverity.High : FindingSeverity.Medium,
                Category: "Impressão",
                Title: $"Impressora offline: {printer.Name}",
                Evidence: $"Status {printer.StatusText}"
                    + (printer.WorkOffline ? ", marcada como \"Usar impressora offline\"" : string.Empty)
                    + $". Porta {printer.PortName ?? "desconhecida"}."
                    + (printer.IsDefault ? " É a impressora padrão." : string.Empty),
                Suggestion: printer.WorkOffline
                    ? "O modo offline está ligado manualmente no Windows. Desmarcar \"Usar impressora offline\" costuma resolver na hora."
                    : "Conferir se o equipamento está ligado e se a porta responde na rede."));
        }
    }

    private static void EvaluateEvents(EvidenceBundle bundle, List<Finding> findings)
    {
        var errors = bundle.Events.Where(e => e.Level == "Erro").ToArray();
        if (errors.Length < EventErrorBurstThreshold) return;

        var topSource = errors
            .GroupBy(e => e.Source, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .First();

        findings.Add(new Finding(
            RuleId: "eventlog.error-burst",
            Severity: FindingSeverity.Medium,
            Category: "Sistema",
            Title: $"{errors.Length} erros recentes no Windows",
            Evidence: $"Origem mais frequente: {topSource.Key} ({topSource.Count()} ocorrências). Último: {topSource.First().Message}",
            Suggestion: "Uma rajada de erros costuma ter uma causa só. Comparar com uma máquina saudável ajuda a isolar."));
    }

    private static void EvaluateRibanenseApps(EvidenceBundle bundle, List<Finding> findings)
    {
        foreach (RibanenseAppInfo app in bundle.RibanenseApps.Where(a => a.RecentErrorCount > 0))
        {
            findings.Add(new Finding(
                RuleId: "ribanense.app-errors",
                Severity: FindingSeverity.Low,
                Category: "Apps Ribanense",
                Title: $"{app.AppId} registrou {app.RecentErrorCount} erro(s)",
                Evidence: app.LastErrorMessage ?? "Sem mensagem detalhada.",
                Suggestion: $"Abrir o app e usar --logs para o histórico completo."));
        }
    }

    internal static bool IsRunning(ServiceInfo service) =>
        string.Equals(service.Status, "Running", StringComparison.OrdinalIgnoreCase);

    internal static string Format(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return string.Create(CultureInfo.CurrentCulture, $"{value:0.#} {units[unit]}");
    }
}
