namespace Ribanense.Solucoes.App.Balanca.Domain;

/// <summary>
/// Resultado de testar uma combinação de configuração serial durante a varredura.
/// </summary>
public sealed record ScanResult(
    SerialConfig Config,
    WeightReading Reading,
    bool Success,
    string? Error = null)
{
    /// <summary>
    /// Pontuação para ranking: leitura estável com peso vale mais que apenas
    /// resposta reconhecida; sem resposta vale zero.
    /// </summary>
    public int Score => Reading.Status switch
    {
        WeightStatus.Estavel => 100,
        WeightStatus.Instavel => 60,
        WeightStatus.Negativo => 50,
        WeightStatus.Sobrecarga => 50,
        _ => 0,
    };
}
