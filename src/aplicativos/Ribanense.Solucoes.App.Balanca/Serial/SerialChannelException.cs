namespace Ribanense.Solucoes.App.Balanca.Serial;

/// <summary>Categoria técnica de uma falha de canal serial, para diferenciar causas na UI/log.</summary>
public enum SerialFault
{
    /// <summary>Porta em uso por outro processo ou acesso negado.</summary>
    Busy,

    /// <summary>Porta inexistente (nome inválido ou dispositivo removido).</summary>
    NotFound,

    /// <summary>Falha de permissão para abrir a porta.</summary>
    AccessDenied,

    /// <summary>Tempo esgotado sem dados.</summary>
    Timeout,

    /// <summary>Erro de linha: paridade, framing ou overrun.</summary>
    LineError,

    /// <summary>Dispositivo desconectado durante a operação.</summary>
    Disconnected,

    /// <summary>Falha não classificada.</summary>
    Unknown,
}

/// <summary>Erro de canal serial com categoria técnica.</summary>
public sealed class SerialChannelException : Exception
{
    public SerialChannelException(SerialFault fault, string message, Exception? inner = null)
        : base(message, inner)
    {
        Fault = fault;
    }

    public SerialFault Fault { get; }

    /// <summary>Descrição amigável em pt-BR da categoria.</summary>
    public string FaultLabel => Fault switch
    {
        SerialFault.Busy => "porta ocupada",
        SerialFault.NotFound => "porta inexistente",
        SerialFault.AccessDenied => "acesso negado",
        SerialFault.Timeout => "timeout",
        SerialFault.LineError => "erro de linha",
        SerialFault.Disconnected => "desconectada",
        _ => "falha",
    };
}
