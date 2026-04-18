using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Ribanense.Solucoes.Launcher.Tests.Helpers;

public static class ZipBuilder
{
    public static byte[] CreateWithManifest(string manifestJson, string entryExecutable = "App.exe")
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifestEntry = archive.CreateEntry("app.json");
            using (var writer = new StreamWriter(manifestEntry.Open(), Encoding.UTF8))
                writer.Write(manifestJson);

            var exeEntry = archive.CreateEntry(entryExecutable);
            using (var s = exeEntry.Open())
                s.Write(new byte[] { 0x4D, 0x5A, 0x90, 0x00 }); // cabeçalho MZ fake
        }
        return ms.ToArray();
    }

    public static string Sha256Hex(byte[] data)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(data)).ToLowerInvariant();
    }

    public static string ShaFileContent(byte[] zip, string zipName)
        => $"{Sha256Hex(zip)}  {zipName}";
}
