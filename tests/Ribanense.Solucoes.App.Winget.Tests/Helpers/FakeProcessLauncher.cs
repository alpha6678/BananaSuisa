using System.Diagnostics;
using Ribanense.Solucoes.App.Winget.Services.Diagnostics;

namespace Ribanense.Solucoes.App.Winget.Tests.Helpers;

public sealed class FakeProcessLauncher : IProcessLauncher
{
    public List<ProcessStartInfo> Started { get; } = new();
    public int ExitCode { get; set; } = 0;
    public bool SimulateUacCancelled { get; set; }
    public string? LogToWrite { get; set; }
    public string? ScriptPathCapture { get; private set; }
    public string? LogPathCapture { get; private set; }

    public Task<int> StartAndWaitAsync(ProcessStartInfo psi, CancellationToken ct)
    {
        Started.Add(psi);

        // Captura o caminho do script e do log a partir dos argumentos.
        foreach (string arg in psi.ArgumentList)
        {
            if (arg.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
            {
                ScriptPathCapture = arg;
                LogPathCapture = System.IO.Path.ChangeExtension(arg, ".log");
                break;
            }
        }

        if (SimulateUacCancelled)
        {
            throw new System.ComponentModel.Win32Exception(ElevatedCommandRunner.UacCancelledExitCode, "user cancelled");
        }

        if (!string.IsNullOrEmpty(LogToWrite) && LogPathCapture is not null)
        {
            System.IO.File.WriteAllText(LogPathCapture, LogToWrite);
        }

        return Task.FromResult(ExitCode);
    }
}
