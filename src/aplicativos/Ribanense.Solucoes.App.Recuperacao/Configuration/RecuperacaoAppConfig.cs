using Ribanense.Solucoes.PluginSDK;

namespace Ribanense.Solucoes.App.Recuperacao.Configuration;

/// <summary>
/// Resolve caminhos do app Recuperação do Windows, respeitando as variáveis
/// injetadas pelo Launcher (RIBANENSE_APP_HOME, RIBANENSE_APP_DATA) com fallback local.
/// </summary>
public static class RecuperacaoAppConfig
{
    public const string AppId = "com.ribanense.recuperacao";

    public static AppPaths Resolve() =>
        AppPaths.Resolve(AppId, vaultFileName: "Recuperacao.dat");
}
