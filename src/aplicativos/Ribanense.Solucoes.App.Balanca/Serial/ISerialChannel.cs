using Ribanense.Solucoes.App.Balanca.Domain;

namespace Ribanense.Solucoes.App.Balanca.Serial;

/// <summary>
/// Abstração de um canal serial. Permite trocar a implementação real
/// (System.IO.Ports) por uma simulada em testes e no modo demo.
/// </summary>
public interface ISerialChannel : IDisposable
{
    bool IsOpen { get; }

    /// <summary>
    /// Abre a porta com os parâmetros informados. Em falha, lança
    /// <see cref="SerialChannelException"/> com a categoria adequada.
    /// </summary>
    void Open(SerialConfig config);

    void Write(ReadOnlySpan<byte> data);

    /// <summary>
    /// Lê até <paramref name="count"/> bytes, aguardando no máximo o ReadTimeout
    /// configurado. Retorna a quantidade lida (0 quando esgota o tempo sem dados).
    /// </summary>
    int Read(byte[] buffer, int offset, int count);

    /// <summary>Descarta o que estiver no buffer de entrada.</summary>
    void DiscardInBuffer();

    void Close();

    /// <summary>
    /// Retorna e limpa o último erro de linha observado (paridade/framing/overrun),
    /// ou <c>null</c> se não houver. Implementações sem esse conceito devolvem null.
    /// </summary>
    string? DrainLineError() => null;
}
