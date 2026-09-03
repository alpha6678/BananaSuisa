using System.IO;
using Ribanense.Solucoes.App.Farol.Domain;

namespace Ribanense.Solucoes.App.Farol.Collectors;

public sealed class DiskCollector : ICollector
{
    public string Id => "disks";
    public string DisplayName => "Discos";

    public Task CollectAsync(EvidenceBundleBuilder builder, CancellationToken ct)
    {
        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            ct.ThrowIfCancellationRequested();

            if (drive.DriveType != DriveType.Fixed) continue;

            try
            {
                if (!drive.IsReady) continue;

                builder.Disks.Add(new DiskInfo(
                    Name: drive.Name,
                    Label: string.IsNullOrWhiteSpace(drive.VolumeLabel) ? null : drive.VolumeLabel,
                    Format: drive.DriveFormat,
                    TotalBytes: drive.TotalSize,
                    FreeBytes: drive.AvailableFreeSpace));
            }
            catch (IOException)
            {
                // Volume que sumiu entre o enumerar e o ler: ignorar em silêncio.
            }
        }

        return Task.CompletedTask;
    }
}
