using System.Diagnostics;
using System.IO;
using Ribanense.Solucoes.PluginSDK;

namespace Ribanense.Solucoes.App.Mago;

/// <summary>Interpretador Python encontrado, com os argumentos que o precedem.</summary>
internal sealed record PythonInterpreter(string FileName, IReadOnlyList<string> PrefixArgs)
{
    internal string DisplayName => PrefixArgs.Count == 0
        ? FileName
        : $"{FileName} {string.Join(' ', PrefixArgs)}";
}

/// <summary>
/// Localiza o Python instalado, sem console (pythonw/pyw quando possível, para
/// a janela Tkinter não abrir um terminal atrás) e inicia o script do app.
/// </summary>
internal static class PythonHost
{
    /// <summary>Teste de versão mínima: 3.8, o menor runtime que o script suporta.</summary>
    internal const string VersionProbe =
        "import sys; sys.exit(0 if sys.version_info >= (3, 8) else 1)";

    internal const string StandaloneExeName = "MagoDaPerformance.exe";

    private static readonly (string FileName, string[] PrefixArgs)[] Candidates =
    {
        ("pythonw", Array.Empty<string>()),
        ("pyw", new[] { "-3" }),
        ("python", Array.Empty<string>()),
        ("py", new[] { "-3" }),
        ("python3", Array.Empty<string>()),
    };

    internal static string ScriptPath =>
        Path.Combine(AppContext.BaseDirectory, "py", Program.ScriptFileName);

    internal static string? ResolveScriptPath() => File.Exists(ScriptPath) ? ScriptPath : null;

    internal static PythonInterpreter? FindInterpreter() => FindInterpreter(Probe);

    /// <summary>
    /// Percorre os candidatos em ordem e devolve o primeiro que responder ao
    /// teste de versão. <paramref name="probe"/> existe para testes.
    /// </summary>
    internal static PythonInterpreter? FindInterpreter(Func<string, string[], bool> probe)
    {
        foreach ((string fileName, string[] prefixArgs) in Candidates)
        {
            if (probe(fileName, prefixArgs))
            {
                return new PythonInterpreter(fileName, prefixArgs);
            }
        }

        return null;
    }

    internal static ProcessStartInfo BuildStartInfo(
        PythonInterpreter python,
        string scriptPath,
        AppPaths paths)
    {
        var psi = new ProcessStartInfo(python.FileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = paths.AppData,
        };

        foreach (string arg in python.PrefixArgs)
        {
            psi.ArgumentList.Add(arg);
        }
        psi.ArgumentList.Add(scriptPath);

        psi.Environment["RIBANENSE_APP_HOME"] = paths.AppHome;
        psi.Environment["RIBANENSE_APP_DATA"] = paths.AppData;
        return psi;
    }

    internal static void Start(PythonInterpreter python, string scriptPath, AppPaths paths)
    {
        using Process? process = Process.Start(BuildStartInfo(python, scriptPath, paths));
        if (process is null)
        {
            throw new InvalidOperationException("O processo do Python não iniciou.");
        }
    }

    /// <summary>Executável congelado pelo PyInstaller, distribuído no zip do release.</summary>
    internal static string StandaloneExePath =>
        Path.Combine(AppContext.BaseDirectory, StandaloneExeName);

    internal static string? ResolveStandaloneExe() =>
        File.Exists(StandaloneExePath) ? StandaloneExePath : null;

    internal static void StartStandalone(string exePath, AppPaths paths)
    {
        using Process? process = Process.Start(BuildStandaloneStartInfo(exePath, paths));
        if (process is null)
        {
            throw new InvalidOperationException("O executável do Mago não iniciou.");
        }
    }

    internal static ProcessStartInfo BuildStandaloneStartInfo(string exePath, AppPaths paths)
    {
        var psi = new ProcessStartInfo(exePath)
        {
            UseShellExecute = false,
            WorkingDirectory = paths.AppData,
        };

        psi.Environment["RIBANENSE_APP_HOME"] = paths.AppHome;
        psi.Environment["RIBANENSE_APP_DATA"] = paths.AppData;
        return psi;
    }

    /// <summary>
    /// Roda <c>--selfcheck</c> no executável congelado e devolve o exit code;
    /// 5 quando não foi possível executar ou o processo estourou o tempo.
    /// </summary>
    internal static int RunSelfCheck(string exePath)
    {
        try
        {
            var psi = new ProcessStartInfo(exePath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("--selfcheck");

            using Process? process = Process.Start(psi);
            if (process is null)
            {
                return 5;
            }

            if (!process.WaitForExit(60000))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return 5;
            }

            return process.ExitCode;
        }
        catch
        {
            return 5;
        }
    }

    private static bool Probe(string fileName, string[] prefixArgs)
    {
        try
        {
            var psi = new ProcessStartInfo(fileName)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            foreach (string arg in prefixArgs)
            {
                psi.ArgumentList.Add(arg);
            }
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add(VersionProbe);

            using Process? process = Process.Start(psi);
            if (process is null)
            {
                return false;
            }

            if (!process.WaitForExit(15000))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
