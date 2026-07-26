using Ribanense.Solucoes.App.Balanca.Domain;

namespace Ribanense.Solucoes.App.Balanca.Protocols;

/// <summary>
/// Protocolo de comunicação com uma balança: como pedir o peso e como interpretar
/// a resposta bruta recebida pela serial.
/// </summary>
public interface IBalancaProtocol
{
    /// <summary>Identificador estável do protocolo (ex.: "toledo").</summary>
    string Key { get; }

    /// <summary>Nome legível do protocolo.</summary>
    string DisplayName { get; }

    /// <summary>
    /// Bytes a enviar para solicitar o peso. Vazio quando a balança envia peso
    /// continuamente (streaming) sem necessidade de requisição.
    /// </summary>
    byte[] BuildRequest();

    /// <summary>
    /// Parâmetros seriais típicos deste protocolo para a porta informada.
    /// Usados como ponto de partida da varredura e do modo manual.
    /// </summary>
    SerialConfig DefaultConfig(string port);

    /// <summary>
    /// Analisa incrementalmente o buffer acumulado. Não interpreta dígitos parciais:
    /// devolve <see cref="FrameParseStatus.NeedMoreData"/> enquanto o frame não estiver
    /// completo, <see cref="FrameParseStatus.FrameParsed"/> quando reconhecer um frame
    /// (informando quantos bytes consumir) e <see cref="FrameParseStatus.InvalidData"/>
    /// para descartar ruído e ressincronizar.
    /// </summary>
    /// <param name="buffer">Bytes acumulados até agora.</param>
    /// <param name="isFinal">
    /// Verdadeiro na última tentativa (timeout), quando o protocolo pode fazer o
    /// melhor esforço com o que houver em vez de continuar aguardando.
    /// </param>
    ProtocolReadResult Read(ReadOnlySpan<byte> buffer, bool isFinal);
}

/// <summary>Conveniências para consumo síncrono (testes e chamadas pontuais).</summary>
public static class BalancaProtocolExtensions
{
    /// <summary>
    /// Executa uma leitura de melhor esforço (isFinal = true) sobre um buffer completo.
    /// Retorna <c>true</c> quando um frame foi reconhecido.
    /// </summary>
    public static bool TryReadWeight(this IBalancaProtocol protocol, ReadOnlySpan<byte> buffer, out WeightReading reading)
    {
        ArgumentNullException.ThrowIfNull(protocol);
        var result = protocol.Read(buffer, isFinal: true);
        reading = result.Reading ?? WeightReading.NotRead();
        return result.Status == FrameParseStatus.FrameParsed;
    }
}
