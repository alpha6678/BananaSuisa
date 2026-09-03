using System.Globalization;
using System.Text;
using Ribanense.Solucoes.App.Farol.Domain;

namespace Ribanense.Solucoes.App.Farol.Analysis;

/// <summary>
/// Explicador padrão: monta um resumo em pt-BR direto dos achados, sem modelo
/// nem dependência externa.
/// </summary>
public sealed class RuleBasedExplainer : IEvidenceExplainer
{
    public Task<string> ExplainAsync(
        EvidenceBundle bundle,
        IReadOnlyList<Finding> findings,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(findings);

        return Task.FromResult(Explain(bundle, findings));
    }

    public static string Explain(EvidenceBundle bundle, IReadOnlyList<Finding> findings)
    {
        var text = new StringBuilder();

        string machine = string.IsNullOrWhiteSpace(bundle.FriendlyName)
            ? bundle.MachineName
            : bundle.FriendlyName;

        text.Append(machine)
            .Append(" — captura de ")
            .Append(bundle.CapturedAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture))
            .AppendLine(".");
        text.AppendLine();

        if (findings.Count == 0)
        {
            text.AppendLine("Nenhum problema conhecido foi encontrado. Todos os sensores responderam e os serviços críticos estão no ar.");
            return text.ToString().TrimEnd();
        }

        text.AppendLine(Headline(findings));
        text.AppendLine();

        foreach (IGrouping<FindingSeverity, Finding> group in findings
            .GroupBy(f => f.Severity)
            .OrderByDescending(g => g.Key))
        {
            text.Append(SeverityLabel(group.Key)).AppendLine(":");

            foreach (Finding finding in group)
            {
                text.Append("- ").Append(finding.Title).Append(". ").AppendLine(finding.Evidence);
                text.Append("  O que fazer: ").AppendLine(finding.Suggestion);
            }

            text.AppendLine();
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>Frase de topo usada também como resumo no card do mapa da LAN.</summary>
    public static string Headline(IReadOnlyList<Finding> findings)
    {
        if (findings.Count == 0) return "Sem problemas detectados.";

        int high = findings.Count(f => f.Severity == FindingSeverity.High);
        int medium = findings.Count(f => f.Severity == FindingSeverity.Medium);

        if (high > 0)
        {
            Finding first = findings.First(f => f.Severity == FindingSeverity.High);
            return high == 1
                ? $"Um problema grave: {first.Title.ToLowerInvariant()}."
                : $"{high} problemas graves, começando por {first.Title.ToLowerInvariant()}.";
        }

        if (medium > 0)
        {
            Finding first = findings.First(f => f.Severity == FindingSeverity.Medium);
            return medium == 1
                ? $"Um ponto de atenção: {first.Title.ToLowerInvariant()}."
                : $"{medium} pontos de atenção, começando por {first.Title.ToLowerInvariant()}.";
        }

        return $"{findings.Count} observação(ões) de baixa severidade.";
    }

    private static string SeverityLabel(FindingSeverity severity) => severity switch
    {
        FindingSeverity.High => "Grave",
        FindingSeverity.Medium => "Atenção",
        FindingSeverity.Low => "Baixo",
        _ => "Informativo",
    };
}
