using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace Ribanense.Solucoes.App.Farol.Services;

public sealed record ElevatedResult(int ExitCode, string Output, bool Cancelled)
{
    public bool Succeeded => !Cancelled && ExitCode == 0;
}

public interface IElevatedScriptRunner
{
    Task<ElevatedResult> RunAsync(string powerShellScript, CancellationToken ct);
}

/// <summary>
/// Executa um script PowerShell elevado por vez, via <c>Verb=runas</c>.
/// </summary>
/// <remarks>
/// O Farol roda sempre como usuário comum. A elevação é pontual e só acontece
/// quando o usuário pede explicitamente (liberar firewall), nunca na inicialização.
/// </remarks>
public sealed class ElevatedScriptRunner : IElevatedScriptRunner
{
    private const int UacCancelledExitCode = 1223;

    public async Task<ElevatedResult> RunAsync(string powerShellScript, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(powerShellScript))
            throw new ArgumentException("Script obrigatório.", nameof(powerShellScript));

        string prefix = "ribanense-farol-" + Guid.NewGuid().ToString("N")[..12];
        string scriptPath = Path.Combine(Path.GetTempPath(), prefix + ".ps1");
        string logPath = Path.Combine(Path.GetTempPath(), prefix + ".log");

        await File.WriteAllTextAsync(scriptPath, Wrap(powerShellScript, logPath), ct).ConfigureAwait(false);

        var psi = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = true, // exigido por Verb=runas
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add(scriptPath);

        int exitCode;
        bool cancelled = false;

        try
        {
            using Process? process = Process.Start(psi);
            if (process is null) return new ElevatedResult(-1, "Não foi possível iniciar o PowerShell.", false);

            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            exitCode = process.ExitCode;
        }
        catch (Win32Exception win32) when (win32.NativeErrorCode == UacCancelledExitCode)
        {
            exitCode = UacCancelledExitCode;
            cancelled = true;
        }
        catch (OperationCanceledException)
        {
            exitCode = -1;
            cancelled = true;
        }

        string output = TryRead(logPath);
        TryDelete(scriptPath);
        TryDelete(logPath);

        return new ElevatedResult(exitCode, output, cancelled);
    }

    internal static string Wrap(string userScript, string logPath)
    {
        string escaped = logPath.Replace("'", "''");

        return "$ErrorActionPreference = 'Continue'" + Environment.NewLine
            + "Start-Transcript -Path '" + escaped + "' -Force | Out-Null" + Environment.NewLine
            + "try {" + Environment.NewLine
            + userScript + Environment.NewLine
            + "    $exit = 0" + Environment.NewLine
            + "} catch {" + Environment.NewLine
            + "    Write-Error $_" + Environment.NewLine
            + "    $exit = 1" + Environment.NewLine
            + "} finally {" + Environment.NewLine
            + "    Stop-Transcript | Out-Null" + Environment.NewLine
            + "}" + Environment.NewLine
            + "exit $exit" + Environment.NewLine;
    }

    private static string TryRead(string path)
    {
        try { return File.Exists(path) ? File.ReadAllText(path) : string.Empty; }
        catch (IOException) { return string.Empty; }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
    }
}
