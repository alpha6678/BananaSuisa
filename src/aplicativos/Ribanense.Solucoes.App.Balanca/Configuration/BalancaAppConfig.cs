using Ribanense.Solucoes.PluginSDK;

namespace Ribanense.Solucoes.App.Balanca.Configuration;

/// <summary>
/// Resolve caminhos do app Testador de Balanças, respeitando as variáveis injetadas
/// pelo Launcher (RIBANENSE_APP_HOME, RIBANENSE_APP_DATA) com fallback local.
/// </summary>
public static class BalancaAppConfig
{
    public const string AppId = "com.ribanense.balanca";

    public static AppPaths Resolve() =>
        AppPaths.Resolve(AppId, vaultFileName: "Balanca.dat");
}
