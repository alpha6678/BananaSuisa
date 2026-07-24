namespace Ribanense.Solucoes.App.Balanca.Domain;

/// <summary>
/// Resultado de uma tentativa de leitura de peso.
/// </summary>
public sealed record WeightReading(
    WeightStatus Status,
    decimal Weight,
    string Unit,
    string RawAscii,
    string RawHex)
{
    /// <summary>Considera-se peso "aproveitável" quando há valor > 0 e estável.</summary>
    public bool IsUsable => Status == WeightStatus.Estavel && Weight > 0m;

    /// <summary>Houve resposta interpretável (mesmo instável/negativo/sobrecarga).</summary>
    public bool HasResponse => Status != WeightStatus.NaoLido;

    public static WeightReading NotRead(string rawAscii = "", string rawHex = "") =>
        new(WeightStatus.NaoLido, 0m, "kg", rawAscii, rawHex);

    public string StatusText => Status switch
    {
        WeightStatus.Estavel => "Estável",
        WeightStatus.Instavel => "Instável",
        WeightStatus.Negativo => "Peso negativo",
        WeightStatus.Sobrecarga => "Sobrecarga",
        _ => "Não lido",
    };
}
