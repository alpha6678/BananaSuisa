namespace Ribanense.Solucoes.App.Scripts.Scripts.Dns;

/// <summary>
/// Detecta configurações de rede locais relevantes para o benchmark de DNS:
/// os servidores DNS atualmente configurados e os nomes de interfaces ativas
/// (usados para oferecer "aplicar o DNS vencedor" via PowerShell).
/// </summary>
public interface INetworkDnsDetector
{
    IReadOnlyList<DnsServerCandidate> DetectCurrent();

    IReadOnlyList<string> DetectActiveInterfaceNames();
}
