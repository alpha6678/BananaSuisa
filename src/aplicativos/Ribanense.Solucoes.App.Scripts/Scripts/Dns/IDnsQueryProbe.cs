namespace Ribanense.Solucoes.App.Scripts.Scripts.Dns;

public sealed record DnsQueryAttempt(bool Success, double ElapsedMs, string? Error);

/// <summary>
/// Envia uma consulta DNS diretamente a um servidor específico (sem passar
/// pelo resolver/cache do sistema operacional), medindo o tempo de resposta.
/// </summary>
public interface IDnsQueryProbe
{
    Task<DnsQueryAttempt> QueryAsync(string serverIp, string domain, TimeSpan timeout, CancellationToken ct);
}
