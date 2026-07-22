using Ribanense.Solucoes.PluginSDK;

namespace Ribanense.Solucoes.App.Sistema.Configuration;

/// <summary>
/// Resolve caminhos do app Gestor de Sistema, respeitando as variáveis injetadas
/// pelo Launcher (RIBANENSE_APP_HOME, RIBANENSE_APP_DATA) com fallback local.
/// </summary>
public static class SistemaAppConfig
{
    public const string AppId = "com.ribanense.sistema";

    public static AppPaths Resolve() =>
        AppPaths.Resolve(AppId, vaultFileName: "Sistema.dat");
}
