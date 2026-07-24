namespace Ribanense.Solucoes.App.Balanca.Domain;

/// <summary>
/// Situação de uma leitura de peso, espelhando os estados clássicos do ACBrBAL.
/// </summary>
public enum WeightStatus
{
    /// <summary>Nenhuma leitura obtida (timeout ou porta muda).</summary>
    NaoLido = 0,

    /// <summary>Peso válido e estável.</summary>
    Estavel = 1,

    /// <summary>Peso oscilando (balança não estabilizou).</summary>
    Instavel = 2,

    /// <summary>Peso negativo reportado pela balança.</summary>
    Negativo = 3,

    /// <summary>Sobrecarga / peso acima da capacidade.</summary>
    Sobrecarga = 4,
}
