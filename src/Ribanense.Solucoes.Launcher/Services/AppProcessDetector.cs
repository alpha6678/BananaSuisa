using System.Threading;

namespace Ribanense.Solucoes.Launcher.Services;

/// <summary>
/// Detecta se um app esta em execucao via mutex nomeado.
/// Convencao: cada app cria <c>Global\Ribanense.{appId}</c> ao iniciar.
/// </summary>
public static class AppProcessDetector
{
    public const string MutexPrefix = @"Global\Ribanense.";

    public static string MutexNameFor(string appId) => MutexPrefix + appId;

    public static bool IsRunning(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId)) return false;

        string name = MutexNameFor(appId);
        try
        {
            using var mutex = Mutex.OpenExisting(name);
            return true;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            // Mutex existe mas sem permissao de abrir; tratamos como "em execucao".
            return true;
        }
    }
}
