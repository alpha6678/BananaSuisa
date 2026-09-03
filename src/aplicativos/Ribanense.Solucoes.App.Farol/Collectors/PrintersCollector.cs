using System.Management;
using Ribanense.Solucoes.App.Farol.Domain;

namespace Ribanense.Solucoes.App.Farol.Collectors;

/// <summary>
/// Filas de impressão via WMI (<c>Win32_Printer</c>), que expõe fila, porta,
/// driver e o sinalizador <c>WorkOffline</c> — a causa mais comum de "sumiu a impressora".
/// </summary>
public sealed class PrintersCollector : ICollector
{
    public string Id => "printers";
    public string DisplayName => "Impressoras";

    public Task CollectAsync(EvidenceBundleBuilder builder, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("WMI indisponível nesta plataforma.");

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, DriverName, PortName, Default, WorkOffline, PrinterStatus, PrinterState "
                + "FROM Win32_Printer");

            foreach (ManagementBaseObject item in searcher.Get())
            {
                ct.ThrowIfCancellationRequested();

                using (item)
                {
                    bool workOffline = GetBool(item, "WorkOffline");
                    int status = GetInt(item, "PrinterStatus");

                    builder.Printers.Add(new PrinterInfo(
                        Name: GetString(item, "Name") ?? "(sem nome)",
                        DriverName: GetString(item, "DriverName"),
                        PortName: GetString(item, "PortName"),
                        IsDefault: GetBool(item, "Default"),
                        IsOffline: workOffline || status == 7,
                        WorkOffline: workOffline,
                        QueuedJobs: 0,
                        StatusText: DescribeStatus(status)));
                }
            }
        }
        catch (ManagementException ex) when (ex.ErrorCode == ManagementStatus.AccessDenied)
        {
            throw new CollectorDeniedException("Sem permissão para consultar impressoras via WMI.", ex);
        }

        return Task.CompletedTask;
    }

    internal static string DescribeStatus(int printerStatus) => printerStatus switch
    {
        1 => "Outro",
        2 => "Desconhecido",
        3 => "Ociosa",
        4 => "Imprimindo",
        5 => "Aquecendo",
        6 => "Impressão parada",
        7 => "Offline",
        _ => "Sem status",
    };

    private static string? GetString(ManagementBaseObject item, string property)
    {
        try { return item[property] as string; }
        catch (ManagementException) { return null; }
    }

    private static bool GetBool(ManagementBaseObject item, string property)
    {
        try { return item[property] is bool value && value; }
        catch (ManagementException) { return false; }
    }

    private static int GetInt(ManagementBaseObject item, string property)
    {
        try { return item[property] is null ? 0 : Convert.ToInt32(item[property]); }
        catch (Exception ex) when (ex is ManagementException or FormatException or InvalidCastException)
        {
            return 0;
        }
    }
}
