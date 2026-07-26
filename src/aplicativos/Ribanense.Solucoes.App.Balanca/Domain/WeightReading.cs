namespace Ribanense.Solucoes.App.Balanca.Domain;

/// <summary>
/// Resultado de uma tentativa de leitura de peso.
/// </summary>
/// <remarks>
/// Três conceitos ficam propositalmente separados:
/// <list type="bullet">
/// <item><see cref="HasResponse"/>: a balança respondeu algo interpretável (mesmo sem número).</item>
/// <item><see cref="HasWeight"/>: a resposta trouxe um valor numérico de peso.</item>
/// <item><see cref="IsUsable"/>: leitura estável com valor numérico (inclui zero estável).</item>
/// </list>
/// Um <c>IIIII</c>/<c>NNNNN</c>/<c>SSSSS</c> é uma resposta válida sem peso, distinta de timeout.
/// </remarks>
public sealed record WeightReading(
    WeightStatus Status,
    decimal Weight,
    string Unit,
    string RawAscii,
    string RawHex,
    bool HasWeight = true)
{
    /// <summary>Leitura estável com valor numérico (zero estável é aproveitável).</summary>
    public bool IsUsable => Status == WeightStatus.Estavel && HasWeight;

    /// <summary>Houve resposta interpretável (mesmo instável/negativo/sobrecarga/sem número).</summary>
    public bool HasResponse => Status != WeightStatus.NaoLido;

    /// <summary>Timeout ou porta muda: nenhuma resposta reconhecível.</summary>
    public static WeightReading NotRead(string rawAscii = "", string rawHex = "") =>
        new(WeightStatus.NaoLido, 0m, "kg", rawAscii, rawHex, HasWeight: false);

    /// <summary>Resposta reconhecida como status (instável/negativo/sobrecarga) sem valor numérico.</summary>
    public static WeightReading StatusOnly(WeightStatus status, string unit, string rawAscii, string rawHex) =>
        new(status, 0m, unit, rawAscii, rawHex, HasWeight: false);

    public string StatusText => Status switch
    {
        WeightStatus.Estavel => "Estável",
        WeightStatus.Instavel => "Instável",
        WeightStatus.Negativo => "Peso negativo",
        WeightStatus.Sobrecarga => "Sobrecarga",
        _ => "Não lido",
    };
}
