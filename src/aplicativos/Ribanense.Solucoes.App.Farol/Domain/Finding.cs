namespace Ribanense.Solucoes.App.Farol.Domain;

public enum FindingSeverity
{
    Info = 0,
    Low = 1,
    Medium = 2,
    High = 3,
}

/// <summary>
/// Achado produzido pelas regras determinísticas. O Farol não corrige: descreve
/// a evidência e sugere o próximo passo humano.
/// </summary>
public sealed record Finding(
    string RuleId,
    FindingSeverity Severity,
    string Category,
    string Title,
    string Evidence,
    string Suggestion);
