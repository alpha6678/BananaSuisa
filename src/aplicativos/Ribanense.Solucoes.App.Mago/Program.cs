using System.Runtime.InteropServices;
using System.Windows;
using Ribanense.Solucoes.PluginSDK;

namespace Ribanense.Solucoes.App.Mago;

/// <summary>
/// Host do MAGO DA PERFORMANCE: cumpre o contrato do Plugin SDK
/// (<c>--version</c>, <c>--selfcheck</c>) e abre o script Python empacotado
/// junto do executável. A interface é a janela Tkinter do próprio script.
/// </summary>
internal static class Program
{
    internal const string AppId = "com.ribanense.mago";
    internal const string ScriptFileName = "mago_da_performance.py";

    private const int AttachParentProcess = -1;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    [STAThread]
    private static int Main(string[] args)
    {
        string command = args.Length > 0 ? args[0].Trim().ToLowerInvariant() : string.Empty;

        if (command == "--version")
        {
            WriteConsole($"{{\"version\":\"{AppVersion.ForEntry()}\",\"sdk\":\"{SdkVersion.Current}\"}}");
            return 0;
        }

        if (command == "--selfcheck")
        {
            return SelfCheck();
        }

        return StartMago();
    }

    /// <summary>
    /// Valida o que o app precisa em runtime: script empacotado, Python 3.8+
    /// e pasta de dados gravável. Exit 0 = íntegro.
    /// </summary>
    private static int SelfCheck()
    {
        string? standalone = PythonHost.ResolveStandaloneExe();
        if (standalone is not null)
        {
            int standaloneExit = PythonHost.RunSelfCheck(standalone);
            WriteConsole(standaloneExit == 0
                ? $"ok: {standalone}"
                : $"selfcheck do executavel standalone retornou {standaloneExit}");
            return standaloneExit;
        }

        string? scriptPath = PythonHost.ResolveScriptPath();
        if (scriptPath is null)
        {
            WriteConsole($"script ausente: {PythonHost.ScriptPath}");
            return 2;
        }

        PythonInterpreter? python = PythonHost.FindInterpreter();
        if (python is null)
        {
            WriteConsole("python 3.8+ ausente (instale e marque 'Add python.exe to PATH')");
            return 3;
        }

        try
        {
            AppPaths paths = AppPaths.Resolve(AppId);
            WriteConsole($"ok: {python.DisplayName} | script {scriptPath} | dados {paths.AppData}");
            return 0;
        }
        catch (Exception ex)
        {
            WriteConsole($"pasta de dados inacessivel: {ex.Message}");
            return 4;
        }
    }

    private static int StartMago()
    {
        AppPaths paths;
        try
        {
            paths = AppPaths.Resolve(AppId);
        }
        catch (Exception ex)
        {
            ShowError($"Não foi possível preparar a pasta de dados do app.\n\n{ex.Message}");
            return 4;
        }

        string? standalone = PythonHost.ResolveStandaloneExe();
        if (standalone is not null)
        {
            try
            {
                PythonHost.StartStandalone(standalone, paths);
                return 0;
            }
            catch (Exception ex)
            {
                ShowError($"Não foi possível abrir o Mago da Performance.\n\n{ex.Message}");
                return 1;
            }
        }

        string? scriptPath = PythonHost.ResolveScriptPath();
        if (scriptPath is null)
        {
            ShowError(
                "O executável do Mago da Performance não foi encontrado nesta instalação.\n\n" +
                "Reinstale o app pelo launcher.");
            return 2;
        }

        PythonInterpreter? python = PythonHost.FindInterpreter();
        if (python is null)
        {
            ShowError(
                "O executável standalone não veio neste pacote e o Python 3.8+ não foi encontrado.\n\n" +
                "Reinstale o app pelo launcher ou instale o Python em\n" +
                "https://www.python.org/downloads/windows/.");
            return 3;
        }

        try
        {
            PythonHost.Start(python, scriptPath, paths);
            return 0;
        }
        catch (Exception ex)
        {
            ShowError($"Não foi possível abrir o Mago da Performance.\n\n{ex.Message}");
            return 1;
        }
    }

    private static void ShowError(string message)
    {
        MessageBox.Show(message, "Mago da Performance", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private static void WriteConsole(string text)
    {
        AttachConsole(AttachParentProcess);
        Console.WriteLine(text);
    }
}
