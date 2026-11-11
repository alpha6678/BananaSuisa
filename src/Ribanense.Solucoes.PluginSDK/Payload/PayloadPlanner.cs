using Ribanense.Solucoes.PluginSDK.Manifest;

namespace Ribanense.Solucoes.PluginSDK.Payload;

public enum PayloadPlanKind
{
    Full,
    Chain
}

/// <summary>Um elo da corrente: o delta publicado em <see cref="ToVersion"/> a partir de <see cref="FromVersion"/>.</summary>
public sealed record DeltaHop(string FromVersion, string ToVersion);

/// <summary>
/// Plano de atualização. <see cref="PayloadPlanKind.Chain"/> com hops vazio significa que
/// a versão instalada já é a de destino. Qualquer elo faltando vira <see cref="PayloadPlanKind.Full"/>.
/// </summary>
public sealed record PayloadPlan(PayloadPlanKind Kind, IReadOnlyList<DeltaHop> Hops)
{
    public static PayloadPlan Full() => new(PayloadPlanKind.Full, Array.Empty<DeltaHop>());

    public bool IsEmptyChain => Kind == PayloadPlanKind.Chain && Hops.Count == 0;
}

/// <summary>Release reduzida ao que o planejador precisa, sem depender do modelo do Launcher.</summary>
public sealed record PayloadReleaseRef(
    string Version,
    bool HasFullPackage,
    IReadOnlyList<string> DeltaFromVersions);

public static class PayloadPlanner
{
    public const int DefaultMaxChain = 8;

    public static PayloadPlan Plan(
        string? installedVersion,
        string targetVersion,
        IReadOnlyList<PayloadReleaseRef> releases,
        int maxChain = DefaultMaxChain)
    {
        if (releases is null) throw new ArgumentNullException(nameof(releases));
        if (maxChain < 1) maxChain = DefaultMaxChain;

        if (string.IsNullOrWhiteSpace(installedVersion) || !SemVerLoose.IsValid(installedVersion))
            return PayloadPlan.Full();
        if (string.IsNullOrWhiteSpace(targetVersion) || !SemVerLoose.IsValid(targetVersion))
            return PayloadPlan.Full();

        if (SemVerLoose.Compare(installedVersion, targetVersion) >= 0)
            return new PayloadPlan(PayloadPlanKind.Chain, Array.Empty<DeltaHop>());

        var usable = releases
            .Where(r => r is not null && SemVerLoose.IsValid(r.Version))
            .GroupBy(r => r.Version, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();

        if (usable.All(r => !string.Equals(r.Version, targetVersion, StringComparison.Ordinal)))
            return PayloadPlan.Full();

        string current = installedVersion;
        var hops = new List<DeltaHop>();

        while (SemVerLoose.Compare(current, targetVersion) < 0)
        {
            PayloadReleaseRef? next = usable
                .Where(r => SemVerLoose.Compare(current, r.Version) < 0)
                .Where(r => SemVerLoose.Compare(r.Version, targetVersion) <= 0)
                .Where(r => r.DeltaFromVersions.Any(v => string.Equals(v, current, StringComparison.Ordinal)))
                .OrderBy(r => r.Version, Comparer<string>.Create(SemVerLoose.Compare))
                .FirstOrDefault();

            if (next is null)
                return PayloadPlan.Full();

            hops.Add(new DeltaHop(current, next.Version));
            current = next.Version;

            if (hops.Count > maxChain)
                return PayloadPlan.Full();
        }

        if (!string.Equals(current, targetVersion, StringComparison.Ordinal))
            return PayloadPlan.Full();

        return new PayloadPlan(PayloadPlanKind.Chain, hops);
    }
}
