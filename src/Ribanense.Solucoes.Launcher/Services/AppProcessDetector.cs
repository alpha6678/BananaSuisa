using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Ribanense.Solucoes.Launcher.Services;

/// <summary>
/// Detecta (e opcionalmente encerra) um app em execucao via mutex nomeado / processo.
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

    /// <summary>
    /// Encerra processos do app (janela principal, depois kill) e espera o mutex liberar.
    /// Retorna <c>true</c> se o app nao estiver mais em execucao.
    /// </summary>
    public static bool TryCloseRunning(string appId, string? executablePath, TimeSpan timeout)
    {
        if (string.IsNullOrWhiteSpace(appId)) return true;
        if (!IsRunning(appId) && FindProcesses(appId, executablePath).Count == 0)
        {
            return true;
        }

        foreach (Process process in FindProcesses(appId, executablePath))
        {
            try
            {
                using (process)
                {
                    if (!process.HasExited)
                    {
                        bool closed = false;
                        try { closed = process.CloseMainWindow(); } catch { }

                        if (!closed || !process.WaitForExit(1_500))
                        {
                            try { process.Kill(entireProcessTree: true); } catch { }
                            try { process.WaitForExit(2_000); } catch { }
                        }
                    }
                }
            }
            catch
            {
                // best effort
            }
        }

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (!IsRunning(appId) && FindProcesses(appId, executablePath).Count == 0)
            {
                return true;
            }
            Thread.Sleep(100);
        }

        foreach (Process process in FindProcesses(appId, executablePath))
        {
            try
            {
                using (process)
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(2_000);
                    }
                }
            }
            catch
            {
                // best effort
            }
        }

        Thread.Sleep(200);
        return !IsRunning(appId) && FindProcesses(appId, executablePath).Count == 0;
    }

    internal static IReadOnlyList<Process> FindProcesses(string appId, string? executablePath)
    {
        var found = new List<Process>();
        string? expectedPath = null;
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            try { expectedPath = Path.GetFullPath(executablePath); } catch { expectedPath = executablePath; }
        }

        string? exeName = expectedPath is null
            ? null
            : Path.GetFileNameWithoutExtension(expectedPath);

        string guessedName = "Ribanense.Solucoes.App." + DeriveAppShortName(appId);

        foreach (Process process in Process.GetProcesses())
        {
            bool match = false;
            try
            {
                if (exeName is not null
                    && string.Equals(process.ProcessName, exeName, StringComparison.OrdinalIgnoreCase))
                {
                    match = true;
                }
                else if (string.Equals(process.ProcessName, guessedName, StringComparison.OrdinalIgnoreCase))
                {
                    match = true;
                }
                else if (expectedPath is not null)
                {
                    string? path = null;
                    try { path = process.MainModule?.FileName; } catch { }
                    if (path is not null
                        && string.Equals(Path.GetFullPath(path), expectedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        match = true;
                    }
                }
            }
            catch
            {
                process.Dispose();
                continue;
            }

            if (match)
            {
                found.Add(process);
            }
            else
            {
                process.Dispose();
            }
        }

        return found;
    }

    private static string DeriveAppShortName(string appId)
    {
        int lastDot = appId.LastIndexOf('.');
        string slug = lastDot >= 0 ? appId[(lastDot + 1)..] : appId;
        if (string.IsNullOrWhiteSpace(slug)) return "App";
        return char.ToUpperInvariant(slug[0]) + slug[1..];
    }
}
