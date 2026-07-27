using Ribanense.Solucoes.PluginSDK;

namespace Ribanense.Solucoes.App.Scripts.Configuration;

/// <summary>
/// Resolve caminhos do app Scripts, respeitando as variáveis injetadas
/// pelo Launcher (RIBANENSE_APP_HOME, RIBANENSE_APP_DATA) com fallback local.
/// </summary>
public static class ScriptsAppConfig
{
    public const string AppId = "com.ribanense.scripts";

    public static AppPaths Resolve() =>
        AppPaths.Resolve(AppId, vaultFileName: "Scripts.dat");
}
