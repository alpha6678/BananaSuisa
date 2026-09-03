using Ribanense.Solucoes.App.Farol.Domain;

namespace Ribanense.Solucoes.App.Farol.Collectors;

/// <summary>
/// Acumulador mutável preenchido pelos coletores. Manter o dossiê imutável e o
/// preenchimento mutável evita passar um record gigante de coletor em coletor.
/// </summary>
public sealed class EvidenceBundleBuilder
{
    public IdentityInfo? Identity { get; set; }
    public NetworkInfo? Network { get; set; }
    public List<DiskInfo> Disks { get; } = new();
    public List<ServiceInfo> Services { get; } = new();
    public List<PrinterInfo> Printers { get; } = new();
    public List<EventEntryInfo> Events { get; } = new();
    public List<RibanenseAppInfo> RibanenseApps { get; } = new();
    public List<ProcessInfo> TopProcesses { get; } = new();
}

/// <summary>
/// Sensor só-leitura. Nenhum coletor pode alterar o estado da máquina, e todos
/// devem tolerar ausência de permissão devolvendo <see cref="CollectorStatus.Denied"/>.
/// </summary>
public interface ICollector
{
    string Id { get; }
    string DisplayName { get; }
    Task CollectAsync(EvidenceBundleBuilder builder, CancellationToken ct);
}

/// <summary>
/// Sinaliza que o coletor não teve permissão. Tratado como <c>Denied</c> e não
/// como falha, porque é um resultado esperado em máquina sem privilégio.
/// </summary>
public sealed class CollectorDeniedException : Exception
{
    public CollectorDeniedException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
