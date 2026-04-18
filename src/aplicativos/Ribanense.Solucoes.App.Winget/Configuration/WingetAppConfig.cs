using System.IO;
using Ribanense.Solucoes.PluginSDK;

namespace Ribanense.Solucoes.App.Winget.Configuration;

/// <summary>
/// Resolve caminhos do app Gestor WinGet, respeitando as variáveis injetadas
/// pelo Launcher (RIBANENSE_APP_HOME, RIBANENSE_APP_DATA) com fallback local.
/// </summary>
public static class WingetAppConfig
{
    public const string AppId = "com.ribanense.winget";

    public static AppPaths Resolve() =>
        AppPaths.Resolve(AppId, vaultFileName: "Winget.dat");
}
