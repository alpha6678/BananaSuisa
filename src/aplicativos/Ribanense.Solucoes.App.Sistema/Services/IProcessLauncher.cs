using System.Diagnostics;

namespace Ribanense.Solucoes.App.Sistema.Services;

public interface IProcessLauncher
{
    Task<int> StartAndWaitAsync(ProcessStartInfo info, CancellationToken ct);
}

internal sealed class ProcessLauncher : IProcessLauncher
{
    public async Task<int> StartAndWaitAsync(ProcessStartInfo info, CancellationToken ct)
    {
        var process = new Process { StartInfo = info, EnableRaisingEvents = true };
        if (!process.Start()) return -1;
        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw;
        }
        return process.ExitCode;
    }
}
