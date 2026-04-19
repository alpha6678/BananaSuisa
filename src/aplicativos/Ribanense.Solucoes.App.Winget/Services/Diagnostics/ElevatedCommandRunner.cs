using System.Diagnostics;
using System.IO;

namespace Ribanense.Solucoes.App.Winget.Services.Diagnostics;

/// <summary>
/// Gera um script PowerShell em <c>%TEMP%</c>, executa via
/// <c>Start-Process -Verb RunAs -Wait</c> (dispara UAC) e le o log produzido
/// para devolver ao chamador. Limpa os arquivos temporarios no final.
/// </summary>
public sealed class ElevatedCommandRunner : IElevatedCommandRunner
{
    // Exit code padrao do Windows quando o usuario cancela o UAC prompt.
    public const int UacCancelledExitCode = 1223;

    private readonly IProcessLauncher _launcher;
    private readonly Func<string> _newGuid;

    public ElevatedCommandRunner(IProcessLauncher? launcher = null, Func<string>? newGuid = null)
    {
        _launcher = launcher ?? new ProcessLauncher();
        _newGuid = newGuid ?? (() => Guid.NewGuid().ToString("N").Substring(0, 12));
    }

    public async Task<ElevatedResult> RunScriptAsync(
        string powerShellScript,
        IProgress<string>? onLine,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(powerShellScript))
            throw new ArgumentException("Script obrigatorio.", nameof(powerShellScript));

        string prefix = "ribanense-elev-" + _newGuid();
        string scriptPath = Path.Combine(Path.GetTempPath(), prefix + ".ps1");
        string logPath = Path.Combine(Path.GetTempPath(), prefix + ".log");

        // Wrap o script para que tudo que ele emite (stdout + stderr) va parar no log.
        string wrapper = BuildWrapper(powerShellScript, logPath);
        await File.WriteAllTextAsync(scriptPath, wrapper, ct).ConfigureAwait(false);

        var psi = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = true, // necessario para Verb=RunAs
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
            exitCode = await _launcher.StartAndWaitAsync(psi, ct).ConfigureAwait(false);
        }
        catch (System.ComponentModel.Win32Exception win32) when (win32.NativeErrorCode == UacCancelledExitCode)
        {
            exitCode = UacCancelledExitCode;
            cancelled = true;
        }
        catch (OperationCanceledException)
        {
            exitCode = -1;
            cancelled = true;
        }

        string output = await TryReadLogAsync(logPath).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(output) && onLine is not null)
        {
            foreach (string line in output.Split('\n'))
            {
                onLine.Report(line.TrimEnd('\r'));
            }
        }

        TryDelete(scriptPath);
        TryDelete(logPath);

        return new ElevatedResult(exitCode, output, cancelled);
    }

    internal static string BuildWrapper(string userScript, string logPath)
    {
        // Escapa o caminho do log para interpolacao segura dentro do ps1 gerado.
        string escapedLog = logPath.Replace("'", "''");

        return "$ErrorActionPreference = 'Continue'" + Environment.NewLine
            + "$RibananseLogPath = '" + escapedLog + "'" + Environment.NewLine
            + "Start-Transcript -Path $RibananseLogPath -Force | Out-Null" + Environment.NewLine
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

    private static async Task<string> TryReadLogAsync(string path)
    {
        try
        {
            if (!File.Exists(path)) return string.Empty;
            return await File.ReadAllTextAsync(path).ConfigureAwait(false);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // best effort
        }
    }
}
