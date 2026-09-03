using System.Diagnostics;
using Ribanense.Solucoes.App.Farol.Domain;

namespace Ribanense.Solucoes.App.Farol.Collectors;

public sealed class ProcessCollector : ICollector
{
    private const int TopCount = 5;

    public string Id => "processes";
    public string DisplayName => "Processos";

    public Task CollectAsync(EvidenceBundleBuilder builder, CancellationToken ct)
    {
        var snapshot = new List<ProcessInfo>();

        foreach (Process process in Process.GetProcesses())
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                snapshot.Add(new ProcessInfo(process.ProcessName, process.Id, process.WorkingSet64));
            }
            catch (InvalidOperationException)
            {
                // Processo encerrou durante a varredura.
            }
            finally
            {
                process.Dispose();
            }
        }

        builder.TopProcesses.AddRange(
            snapshot.OrderByDescending(p => p.WorkingSetBytes).Take(TopCount));

        return Task.CompletedTask;
    }
}
