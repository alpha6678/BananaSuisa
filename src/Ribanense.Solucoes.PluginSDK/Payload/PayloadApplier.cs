using System.IO.Compression;

namespace Ribanense.Solucoes.PluginSDK.Payload;

/// <summary>
/// Copia, extrai e aplica deltas em disco. Rejeita entradas de zip que saem da pasta de destino.
/// </summary>
public static class PayloadApplier
{
    public static void ExtractZip(byte[] zipBytes, string destination) =>
        ExtractZip(new MemoryStream(zipBytes), destination);

    public static void ExtractZip(Stream zipStream, string destination)
    {
        if (zipStream is null) throw new ArgumentNullException(nameof(zipStream));
        if (string.IsNullOrWhiteSpace(destination)) throw new ArgumentException("Destino obrigatório.", nameof(destination));

        Directory.CreateDirectory(destination);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                string dir = CombineSafe(destination, entry.FullName.TrimEnd('/', '\\'));
                if (dir.Length > 0) Directory.CreateDirectory(dir);
                continue;
            }

            string target = CombineSafe(destination, entry.FullName);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
        }
    }

    public static void CopyTree(string source, string destination)
    {
        if (!Directory.Exists(source))
            throw new DirectoryNotFoundException(source);

        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, file);
            if (ShouldSkipCopy(relative)) continue;

            string target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    /// <summary>
    /// Copia só o payload (arquivos do manifesto + o manifesto). Sem manifesto, copia a pasta
    /// inteira exceto dados do usuário e sobras de atualização.
    /// </summary>
    public static void CopyDeployment(string sourceDir, string destinationDir)
    {
        string manifestPath = Path.Combine(sourceDir, PayloadManifest.FileName);
        Directory.CreateDirectory(destinationDir);

        if (File.Exists(manifestPath))
        {
            PayloadManifest manifest = PayloadManifest.Load(manifestPath);
            foreach (PayloadFileEntry file in manifest.Files)
            {
                string from = Path.Combine(sourceDir, file.Path.Replace('/', Path.DirectorySeparatorChar));
                string to = Path.Combine(destinationDir, file.Path.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(from))
                    throw new FileNotFoundException($"Arquivo do manifesto ausente: {file.Path}", from);
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                File.Copy(from, to, overwrite: true);
            }

            File.Copy(manifestPath, Path.Combine(destinationDir, PayloadManifest.FileName), overwrite: true);
            return;
        }

        CopyTree(sourceDir, destinationDir);
    }

    public static void PreserveSidecars(string sourceDir, string stagingDir)
    {
        if (!Directory.Exists(sourceDir)) return;

        foreach (string name in PayloadManifest.PreservedDirectoryNames)
        {
            string source = Path.Combine(sourceDir, name);
            string target = Path.Combine(stagingDir, name);
            if (Directory.Exists(source) && !Directory.Exists(target))
                CopyTree(source, target);
        }

        string flag = Path.Combine(sourceDir, PayloadManifest.PortableMarkerFileName);
        if (File.Exists(flag))
        {
            File.Copy(flag, Path.Combine(stagingDir, PayloadManifest.PortableMarkerFileName), overwrite: true);
        }

        string mode = Path.Combine(sourceDir, PayloadManifest.InstallModeFileName);
        if (File.Exists(mode))
        {
            File.Copy(mode, Path.Combine(stagingDir, PayloadManifest.InstallModeFileName), overwrite: true);
        }
    }

    public static void ApplyDelta(string stagingDir, byte[] deltaZip, DeltaManifest manifest)
    {
        if (manifest is null) throw new ArgumentNullException(nameof(manifest));
        ValidateDeltaPaths(manifest);
        ExtractZip(deltaZip, stagingDir);

        foreach (string deleted in manifest.Deleted)
        {
            string full = Path.Combine(stagingDir, deleted.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(full)) File.Delete(full);
        }

        var errors = VerifyEntries(stagingDir, manifest.Files);
        if (errors.Count > 0)
            throw new InvalidDataException("Delta não confere: " + string.Join("; ", errors));
    }

    public static IReadOnlyList<string> Verify(string root, PayloadManifest manifest)
    {
        if (manifest is null) throw new ArgumentNullException(nameof(manifest));
        return VerifyEntries(root, manifest.Files);
    }

    public static byte[] CreateDeltaZip(string targetRoot, DeltaManifest delta)
    {
        if (delta is null) throw new ArgumentNullException(nameof(delta));

        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (PayloadFileEntry file in delta.Files)
            {
                AddFile(archive, targetRoot, file.Path);
            }

            string manifestPath = Path.Combine(targetRoot, PayloadManifest.FileName);
            if (File.Exists(manifestPath) &&
                delta.Files.All(f => !string.Equals(f.Path, PayloadManifest.FileName, StringComparison.OrdinalIgnoreCase)))
            {
                AddFile(archive, targetRoot, PayloadManifest.FileName);
            }
        }

        return ms.ToArray();
    }

    public static void ResetDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
        Directory.CreateDirectory(path);
    }

    private static void AddFile(ZipArchive archive, string root, string relative)
    {
        string source = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(source))
            throw new FileNotFoundException($"Arquivo do delta ausente: {relative}", source);

        ZipArchiveEntry entry = archive.CreateEntry(relative.Replace('\\', '/'), CompressionLevel.Optimal);
        using Stream target = entry.Open();
        using FileStream input = File.OpenRead(source);
        input.CopyTo(target);
    }

    private static List<string> VerifyEntries(string root, IEnumerable<PayloadFileEntry> files)
    {
        var errors = new List<string>();
        foreach (PayloadFileEntry file in files)
        {
            if (!PayloadManifest.IsPayloadPath(file.Path) &&
                !string.Equals(file.Path, PayloadManifest.FileName, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"caminho recusado: {file.Path}");
                continue;
            }

            string full = Path.Combine(root, file.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full))
            {
                errors.Add($"ausente: {file.Path}");
                continue;
            }

            var info = new FileInfo(full);
            if (info.Length != file.Size)
            {
                errors.Add($"tamanho: {file.Path}");
                continue;
            }

            string hash = PayloadManifest.HashFile(full);
            if (!hash.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
                errors.Add($"hash: {file.Path}");
        }

        return errors;
    }

    private static void ValidateDeltaPaths(DeltaManifest manifest)
    {
        foreach (PayloadFileEntry file in manifest.Files)
        {
            if (!PayloadManifest.IsPayloadPath(file.Path))
                throw new InvalidDataException($"Caminho de delta recusado: {file.Path}");
        }

        foreach (string deleted in manifest.Deleted)
        {
            if (!PayloadManifest.IsPayloadPath(deleted))
                throw new InvalidDataException($"Remoção de delta recusada: {deleted}");
        }
    }

    private static bool ShouldSkipCopy(string relative)
    {
        string normalized = relative.Replace('\\', '/');
        if (normalized.Contains("/.old-", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(".old-", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains(".new-", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    internal static string CombineSafe(string root, string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName))
            throw new InvalidDataException("Entrada de zip vazia.");

        string normalized = entryName.Replace('\\', '/').TrimStart('/');
        if (Path.IsPathRooted(entryName) || normalized.Contains(':'))
            throw new InvalidDataException($"Caminho absoluto no zip: {entryName}");

        string[] parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            throw new InvalidDataException($"Entrada de zip inválida: {entryName}");
        if (parts.Any(p => p == ".."))
            throw new InvalidDataException($"Caminho inseguro no zip: {entryName}");

        string combined = Path.GetFullPath(root);
        foreach (string part in parts)
            combined = Path.Combine(combined, part);

        string full = Path.GetFullPath(combined);
        string rootFull = Path.GetFullPath(root);
        string rootPrefix = rootFull.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(full, rootFull, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Caminho fora do destino: {entryName}");
        }

        return full;
    }
}
