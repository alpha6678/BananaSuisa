using Ribanense.Solucoes.App.Balanca.Protocols;

namespace Ribanense.Solucoes.App.Balanca.Domain;

/// <summary>
/// Modelo de balança exibido ao usuário. Cada modelo aponta para um protocolo
/// (preciso ou genérico) e conhece sua configuração serial típica.
/// </summary>
public sealed class BalancaModel
{
    public BalancaModel(string key, string displayName, IBalancaProtocol protocol, bool isSimulated = false)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
        IsSimulated = isSimulated;
    }

    public string Key { get; }
    public string DisplayName { get; }
    public IBalancaProtocol Protocol { get; }

    /// <summary>Modelo virtual usado para testar o app sem hardware.</summary>
    public bool IsSimulated { get; }

    public SerialConfig DefaultConfig(string port) => Protocol.DefaultConfig(port);

    public override string ToString() => DisplayName;
}
