using System.ComponentModel;
using System.ServiceProcess;
using Ribanense.Solucoes.App.Farol.Domain;

namespace Ribanense.Solucoes.App.Farol.Collectors;

/// <summary>
/// Estado dos serviços que costumam explicar as falhas do dia a dia:
/// fila de impressão, compartilhamento de rede, cache DNS e WMI.
/// </summary>
public sealed class ServicesCollector : ICollector
{
    public static readonly IReadOnlyList<string> WatchedServices = new[]
    {
        "Spooler",
        "LanmanServer",
        "LanmanWorkstation",
        "Dnscache",
        "Winmgmt",
    };

    private readonly IReadOnlyList<string> _names;

    public ServicesCollector(IReadOnlyList<string>? names = null)
    {
        _names = names ?? WatchedServices;
    }

    public string Id => "services";
    public string DisplayName => "Serviços críticos";

    public Task CollectAsync(EvidenceBundleBuilder builder, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Serviços do Windows indisponíveis nesta plataforma.");

        foreach (string name in _names)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                using var controller = new ServiceController(name);
                builder.Services.Add(new ServiceInfo(
                    Name: controller.ServiceName,
                    DisplayName: controller.DisplayName,
                    Status: controller.Status.ToString(),
                    StartType: controller.StartType.ToString()));
            }
            catch (InvalidOperationException ex) when (ex.InnerException is Win32Exception win32 && win32.NativeErrorCode == 5)
            {
                throw new CollectorDeniedException($"Sem permissão para consultar o serviço {name}.", ex);
            }
            catch (InvalidOperationException)
            {
                // Serviço inexistente nesta edição do Windows: não é erro.
            }
        }

        return Task.CompletedTask;
    }
}
