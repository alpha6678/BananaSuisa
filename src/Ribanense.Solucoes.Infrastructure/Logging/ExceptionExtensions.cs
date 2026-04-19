using System.Text;

namespace Ribanense.Solucoes.Infrastructure.Logging;

public static class ExceptionExtensions
{
    /// <summary>
    /// Concatena a mensagem da exceção com a cadeia de InnerExceptions,
    /// no formato <c>[TipoExterno] msg -> [TipoInterno1] msg -> ...</c>.
    /// Útil para o campo Message de um log estruturado, sem exigir o
    /// stack trace completo.
    /// </summary>
    public static string ToChainedMessage(this Exception? exception, int maxDepth = 10)
    {
        if (exception is null) return string.Empty;

        var sb = new StringBuilder();
        var current = exception;
        int depth = 0;

        while (current is not null && depth < maxDepth)
        {
            if (depth > 0) sb.Append(" -> ");
            sb.Append('[').Append(current.GetType().Name).Append("] ").Append(current.Message);
            current = current.InnerException;
            depth++;
        }

        if (current is not null)
        {
            sb.Append(" -> ...");
        }

        return sb.ToString();
    }
}
