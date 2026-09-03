using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using Ribanense.Solucoes.App.Farol.Analysis;
using Ribanense.Solucoes.App.Farol.Domain;
using Ribanense.Solucoes.App.Farol.Mesh;

namespace Ribanense.Solucoes.App.Farol.Services;

/// <summary>
/// Empacota dossiê, achados, linha do tempo dos pares e um resumo legível em um
/// ZIP único — o arquivo que o técnico anexa no chamado ou manda no WhatsApp.
/// </summary>
public sealed class BundleExporter
{
    public static string SuggestFileName(EvidenceBundle bundle)
    {
        string host = Sanitize(string.IsNullOrWhiteSpace(bundle.MachineName)
            ? Environment.MachineName
            : bundle.MachineName);

        return string.Create(
            CultureInfo.InvariantCulture,
            $"farol-{host}-{bundle.CapturedAt:yyyyMMdd-HHmm}.zip");
    }

    public string Export(
        string destinationPath,
        EvidenceBundle bundle,
        IReadOnlyList<Finding> findings,
        IReadOnlyList<PeerBeacon> peers)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(peers);

        string? directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        using var archive = new ZipArchive(
            File.Create(destinationPath),
            ZipArchiveMode.Create,
            leaveOpen: false,
            Encoding.UTF8);

        WriteEntry(archive, "bundle.json", FarolJson.Serialize(bundle));
        WriteEntry(archive, "findings.json", FarolJson.Serialize(findings));
        WriteEntry(archive, "peers-timeline.json", FarolJson.Serialize(BuildTimeline(peers)));
        WriteEntry(archive, "resumo.txt", RuleBasedExplainer.Explain(bundle, findings));

        return destinationPath;
    }

    private static IReadOnlyList<object> BuildTimeline(IReadOnlyList<PeerBeacon> peers)
    {
        DateTimeOffset now = DateTimeOffset.Now;

        return peers
            .OrderByDescending(p => p.LastSeen)
            .Select(object (p) => new
            {
                p.MachineId,
                p.MachineName,
                p.FriendlyName,
                p.Address,
                p.Version,
                p.LastSeen,
                State = p.StateAt(now, PeerRegistry.AbsentAfter, PeerRegistry.OfflineAfter).ToString(),
                SilentForMinutes = Math.Round((now - p.LastSeen).TotalMinutes, 1),
                Health = p.LastHealth,
            })
            .ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }

    private static string Sanitize(string value)
    {
        var clean = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            clean.Append(char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-');
        }
        return clean.ToString();
    }
}
