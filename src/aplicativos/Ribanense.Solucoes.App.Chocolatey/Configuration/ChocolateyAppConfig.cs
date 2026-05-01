using Ribanense.Solucoes.PluginSDK;

namespace Ribanense.Solucoes.App.Chocolatey.Configuration;

/// <summary>
/// Resolve caminhos do app Gestor Chocolatey, respeitando as variáveis injetadas
/// pelo Launcher com fallback local.
/// </summary>
public static class ChocolateyAppConfig
{
    public const string AppId = "com.ribanense.chocolatey";

    public static AppPaths Resolve() =>
        AppPaths.Resolve(AppId, vaultFileName: "Chocolatey.dat");
}
