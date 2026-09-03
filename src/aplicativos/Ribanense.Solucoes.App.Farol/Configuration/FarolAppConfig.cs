using Ribanense.Solucoes.PluginSDK;

namespace Ribanense.Solucoes.App.Farol.Configuration;

/// <summary>
/// Resolve caminhos do app Farol, respeitando as variáveis injetadas
/// pelo Launcher (RIBANENSE_APP_HOME, RIBANENSE_APP_DATA) com fallback local.
/// </summary>
public static class FarolAppConfig
{
    public const string AppId = "com.ribanense.farol";

    /// <summary>Porta UDP de descoberta de faróis na LAN.</summary>
    public const int DiscoveryPort = 38400;

    /// <summary>Porta TCP da API HTTP entre pares.</summary>
    public const int PeerPort = 38401;

    public static AppPaths Resolve() =>
        AppPaths.Resolve(AppId, vaultFileName: "Farol.dat");
}
