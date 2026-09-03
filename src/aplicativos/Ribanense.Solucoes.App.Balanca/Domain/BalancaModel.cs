using Ribanense.Solucoes.App.Balanca.Protocols;

namespace Ribanense.Solucoes.App.Balanca.Domain;

/// <summary>Nível de confiança do suporte a um modelo.</summary>
public enum ModelSupport
{
    /// <summary>Protocolo com formato documentado (manual/fixture verificável).</summary>
    Documentado,

    /// <summary>Reconhecido apenas por heurística genérica; não homologado.</summary>
    Experimental,
}

/// <summary>
/// Modelo de balança exibido ao usuário. Cada modelo aponta para um protocolo
/// (preciso ou genérico) e conhece sua configuração serial típica.
/// </summary>
public sealed class BalancaModel
{
    public BalancaModel(
        string key,
        string displayName,
        IBalancaProtocol protocol,
        ModelSupport support = ModelSupport.Documentado,
        bool isSimulated = false,
        string notes = "")
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
        Support = support;
        IsSimulated = isSimulated;
        Notes = notes ?? "";
    }

    public string Key { get; }
    public string DisplayName { get; }
    public IBalancaProtocol Protocol { get; }
    public ModelSupport Support { get; }

    /// <summary>Resumo do protocolo e da configuração típica, exibido junto ao modelo.</summary>
    public string Notes { get; }

    /// <summary>Modelo virtual usado para testar o app sem hardware.</summary>
    public bool IsSimulated { get; }

    /// <summary>Rótulo para a interface, sinalizando modelos experimentais.</summary>
    public string Label => Support == ModelSupport.Experimental
        ? $"{DisplayName} — experimental"
        : DisplayName;

    public SerialConfig DefaultConfig(string port) => Protocol.DefaultConfig(port);

    public override string ToString() => Label;
}
