using System.Text;

namespace BananaSuisa.Infrastructure.Provisioning;

internal static class PowerShellInvoker
{
    public static async Task<ProcessRunResult> RunScriptAsync(string script, CancellationToken cancellationToken)
    {
        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        string ps = ResolvePowerShell();
        return await ProcessRunner.RunAsync(
            ps,
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}",
            cancellationToken).ConfigureAwait(false);
    }

    public static string ResolvePowerShell()
    {
        string pwsh = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe");
        if (File.Exists(pwsh))
        {
            return pwsh;
        }

        string sysRoot = Environment.GetFolderPath(Environment.SpecialFolder.System);
        return Path.Combine(sysRoot, "WindowsPowerShell", "v1.0", "powershell.exe");
    }
}
