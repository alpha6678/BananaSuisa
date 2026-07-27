using Ribanense.Solucoes.App.Scripts.Scripts.Commands;

namespace Ribanense.Solucoes.App.Scripts.Scripts.Dns;

/// <summary>
/// Monta o comando PowerShell (elevado) que aplica um ou mais servidores DNS
/// a uma interface de rede específica.
/// </summary>
public static class DnsApplyCommandBuilder
{
    public static ShellCommandStep Build(string interfaceAlias, IReadOnlyList<string> dnsServers)
    {
        if (string.IsNullOrWhiteSpace(interfaceAlias))
            throw new ArgumentException("Nome da interface de rede obrigatório.", nameof(interfaceAlias));
        if (dnsServers is null || dnsServers.Count == 0)
            throw new ArgumentException("Informe ao menos um servidor DNS.", nameof(dnsServers));

        string serversLiteral = string.Join(",", dnsServers.Select(ip => $"'{ip}'"));
        string command =
            $"Set-DnsClientServerAddress -InterfaceAlias '{interfaceAlias}' -ServerAddresses @({serversLiteral})";

        return new ShellCommandStep(
            Description: $"Aplicar DNS {string.Join(", ", dnsServers)} na interface \"{interfaceAlias}\"",
            Executable: "powershell.exe",
            Arguments: new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", command },
            RequiresElevation: true);
    }
}
