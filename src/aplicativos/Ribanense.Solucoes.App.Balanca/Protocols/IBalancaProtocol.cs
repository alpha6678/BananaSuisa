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
    /// Tenta interpretar o buffer recebido como uma leitura de peso.
    /// Retorna false quando o buffer ainda não contém um frame reconhecível.
    /// </summary>
    bool TryParse(ReadOnlySpan<byte> buffer, out WeightReading reading);
}
