namespace Ribanense.Solucoes.App.Winget.Services.Diagnostics;

public sealed record PowerShellResult(int ExitCode, string Stdout, string Stderr)
{
    public bool Success => ExitCode == 0;
}

/// <summary>
/// Executa um comando PowerShell nao-elevado e devolve stdout/stderr.
/// Usado pelo diagnostico para chamar <c>Get-AppxPackage</c> sem UAC.
/// </summary>
public interface IPowerShellRunner
{
    Task<PowerShellResult> RunAsync(string command, CancellationToken ct);
}

public sealed class PowerShellRunner : IPowerShellRunner
{
    public async Task<PowerShellResult> RunAsync(string command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException("command obrigatorio.", nameof(command));

        var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add(command);

        using var p = new System.Diagnostics.Process { StartInfo = psi };
        p.Start();
        string stdoutTask = await p.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        string stderrTask = await p.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await p.WaitForExitAsync(ct).ConfigureAwait(false);

        return new PowerShellResult(p.ExitCode, stdoutTask, stderrTask);
    }
}
