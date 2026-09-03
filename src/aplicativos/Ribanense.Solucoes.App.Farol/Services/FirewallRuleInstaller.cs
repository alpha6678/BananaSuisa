using System.Globalization;

namespace Ribanense.Solucoes.App.Farol.Services;

/// <summary>
/// Cria as regras de entrada que a malha precisa: descoberta UDP e API TCP,
/// ambas restritas ao escopo local da rede.
/// </summary>
/// <remarks>
/// Uma única elevação cria as duas regras. <c>-Profile Domain,Private</c> e
/// <c>-RemoteAddress LocalSubnet</c> são deliberados: em rede Pública o Farol
/// deve continuar mudo, e nada aqui abre porta para fora da sub-rede.
/// </remarks>
public sealed class FirewallRuleInstaller
{
    public const string DiscoveryRuleName = "Ribanense Farol - Descoberta (UDP)";
    public const string PeerRuleName = "Ribanense Farol - API entre pares (TCP)";

    private readonly IElevatedScriptRunner _runner;
    private readonly int _discoveryPort;
    private readonly int _peerPort;

    public FirewallRuleInstaller(IElevatedScriptRunner runner, int discoveryPort, int peerPort)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _discoveryPort = discoveryPort;
        _peerPort = peerPort;
    }

    public Task<ElevatedResult> InstallAsync(CancellationToken ct) =>
        _runner.RunAsync(BuildInstallScript(_discoveryPort, _peerPort), ct);

    public Task<ElevatedResult> RemoveAsync(CancellationToken ct) =>
        _runner.RunAsync(BuildRemoveScript(), ct);

    // Os scripts são templates literais com marcadores, e não strings
    // interpoladas: PowerShell usa `$` e `{}` o tempo todo, e misturar as duas
    // sintaxes de template torna o script ilegível.
    private const string InstallTemplate = """
        $rules = @(
            @{ Name = '%UDP_RULE%'; Protocol = 'UDP'; Port = %UDP_PORT% },
            @{ Name = '%TCP_RULE%'; Protocol = 'TCP'; Port = %TCP_PORT% }
        )

        foreach ($rule in $rules) {
            Get-NetFirewallRule -DisplayName $rule.Name -ErrorAction SilentlyContinue |
                Remove-NetFirewallRule -ErrorAction SilentlyContinue

            New-NetFirewallRule -DisplayName $rule.Name -Direction Inbound -Action Allow -Protocol $rule.Protocol -LocalPort $rule.Port -Profile Domain,Private -RemoteAddress LocalSubnet -Description 'Malha de evidencias do Ribanense Farol na rede local.' | Out-Null

            Write-Output ("Regra criada: " + $rule.Name)
        }
        """;

    private const string RemoveTemplate = """
        foreach ($name in @('%UDP_RULE%', '%TCP_RULE%')) {
            Get-NetFirewallRule -DisplayName $name -ErrorAction SilentlyContinue |
                Remove-NetFirewallRule -ErrorAction SilentlyContinue
            Write-Output ("Regra removida: " + $name)
        }
        """;

    internal static string BuildInstallScript(int discoveryPort, int peerPort) =>
        InstallTemplate
            .Replace("%UDP_RULE%", DiscoveryRuleName)
            .Replace("%TCP_RULE%", PeerRuleName)
            .Replace("%UDP_PORT%", discoveryPort.ToString(CultureInfo.InvariantCulture))
            .Replace("%TCP_PORT%", peerPort.ToString(CultureInfo.InvariantCulture));

    internal static string BuildRemoveScript() =>
        RemoveTemplate
            .Replace("%UDP_RULE%", DiscoveryRuleName)
            .Replace("%TCP_RULE%", PeerRuleName);
}
