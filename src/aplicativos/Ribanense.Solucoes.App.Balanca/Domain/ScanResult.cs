using Ribanense.Solucoes.App.Balanca.Protocols;

namespace Ribanense.Solucoes.App.Balanca.Domain;

/// <summary>
/// Resultado de testar uma combinação de configuração serial durante a varredura.
/// </summary>
public sealed record ScanResult(
    SerialConfig Config,
    WeightReading Reading,
    bool Success,
    FrameConfidence Confidence = FrameConfidence.None,
    string? Error = null)
{
    /// <summary>
    /// Pontuação para ranking. Prioriza a confiança do frame (delimitado/documentado
    /// vale mais que texto salvo por heurística) e, dentro dela, o status da leitura.
    /// Sem resposta vale zero.
    /// </summary>
    public int Score
    {
        get
        {
            int statusScore = Reading.Status switch
            {
                WeightStatus.Estavel => 100,
                WeightStatus.Instavel => 60,
                WeightStatus.Negativo => 50,
                WeightStatus.Sobrecarga => 50,
                _ => 0,
            };
            if (statusScore == 0) return 0;
            return statusScore + (int)Confidence * 15;
        }
    }
}
