using Ribanense.Solucoes.App.Farol.Domain;

namespace Ribanense.Solucoes.App.Farol.Analysis;

/// <summary>
/// Traduz dossiê e achados em texto para humano.
/// </summary>
/// <remarks>
/// Este é o ponto de extensão reservado para a camada de IA local. A troca por
/// um explicador baseado em modelo não pode alterar coleta nem regras: se o
/// explicador falhar, o Farol continua capturando e listando achados.
/// </remarks>
public interface IEvidenceExplainer
{
    Task<string> ExplainAsync(
        EvidenceBundle bundle,
        IReadOnlyList<Finding> findings,
        CancellationToken ct);
}
