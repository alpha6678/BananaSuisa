using Ribanense.Solucoes.App.Balanca.Domain;

namespace Ribanense.Solucoes.App.Balanca.Serial;

/// <summary>
/// Abstração de um canal serial. Permite trocar a implementação real
/// (System.IO.Ports) por uma simulada em testes e no modo demo.
/// </summary>
public interface ISerialChannel : IDisposable
{
    bool IsOpen { get; }

    /// <summary>Abre a porta com os parâmetros informados. Lança em caso de falha.</summary>
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
}
