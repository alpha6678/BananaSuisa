using System.Diagnostics;
using System.IO;
using Ribanense.Solucoes.Launcher.Domain;
using Ribanense.Solucoes.PluginSDK.Manifest;

namespace Ribanense.Solucoes.Launcher.Services;

/// <summary>
/// Acha a cópia de um projeto parceiro: pasta do launcher, registro de desinstalação
/// ou um dos caminhos declarados. A versão vem do app.json, do registro, de
/// <c>--version</c> ou do FileVersion.
/// </summary>
public sealed class PartnerInstallProbe
{
    private readonly IInstallRegistration _registration;
    private readonly Func<string, string?> _versionProbe;

    public PartnerInstallProbe(IInstallRegistration registration, Func<string, string?>? versionProbe = null)
    {
        _registration = registration ?? throw new ArgumentNullException(nameof(registration));
        _versionProbe = versionProbe ?? ProbeProcessVersion;
    }

    public InstalledApp? Probe(CatalogEntry entry, string aplicativosRoot)
    {
        if (entry is null || !entry.IsPartner) return null;

        string? managedDir = string.IsNullOrWhiteSpace(aplicativosRoot)
            ? null
            : Path.Combine(aplicativosRoot, AppSlug.FromId(entry.Id));
        if (TryFromDirectory(entry, managedDir, managedByLauncher: true) is InstalledApp managed)
            return managed;

        if (!string.IsNullOrWhiteSpace(entry.UninstallRegistryName))
        {
            foreach (InstallRegistration registration in _registration.FindByDisplayName(entry.UninstallRegistryName))
            {
                if (TryFromDirectory(entry, registration.InstallLocation, managedByLauncher: false, registration.DisplayVersion) is InstalledApp external)
                    return external;
            }
        }

        foreach (string candidate in entry.InstallPathCandidates)
        {
            string expanded = Environment.ExpandEnvironmentVariables(candidate);
            if (TryFromDirectory(entry, expanded, managedByLauncher: false) is InstalledApp found)
                return found;
        }

        return null;
    }

    private InstalledApp? TryFromDirectory(
        CatalogEntry entry,
        string? directory,
        bool managedByLauncher,
        string? fallbackVersion = null)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return null;

        string exeName = entry.EntryExecutable ?? string.Empty;
        string? exePath = null;
        AppManifest? manifest = null;
        string manifestPath = Path.Combine(directory, "app.json");
        if (File.Exists(manifestPath))
        {
            try
            {
                manifest = AppManifest.Load(manifestPath);
                if (manifest.Validate().Count == 0 && !string.IsNullOrWhiteSpace(manifest.EntryExecutable))
                    exeName = manifest.EntryExecutable;
            }
            catch
            {
                manifest = null;
            }
        }

        if (!string.IsNullOrWhiteSpace(exeName))
        {
            string candidate = Path.Combine(directory, exeName);
            if (File.Exists(candidate)) exePath = candidate;
        }

        exePath ??= Directory.EnumerateFiles(directory, "*.exe").FirstOrDefault();
        if (exePath is null) return null;

        string? version = manifest is not null ? AppInstallService.NormalizeVersion(manifest.Version) : null;
        version ??= AppInstallService.NormalizeVersion(fallbackVersion);
        version ??= AppInstallService.NormalizeVersion(_versionProbe(exePath));
        version ??= ReadFileVersion(exePath);
        if (version is null) return null;

        AppManifest effective = manifest ?? AppInstallService.PartnerManifest(entry, version, Path.GetFileName(exePath));
        if (!string.Equals(effective.Version, version, StringComparison.Ordinal))
        {
            effective = AppInstallService.PartnerManifest(entry, version, Path.GetFileName(exePath));
        }

        return new InstalledApp
        {
            Manifest = effective,
            InstallPath = Path.GetFullPath(directory),
            ExecutablePath = Path.GetFullPath(exePath),
            Mode = managedByLauncher ? InstallRegistrationService.ReadMode(directory!) : AppInstallMode.Registered,
            ManagedByLauncher = managedByLauncher
        };
    }

    private static string? ProbeProcessVersion(string exePath)
    {
        try
        {
            var psi = new ProcessStartInfo(exePath, "--version")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using Process? process = Process.Start(psi);
            if (process is null) return null;
            if (!process.WaitForExit(4000))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return null;
            }

            return process.StandardOutput.ReadToEnd();
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadFileVersion(string exePath)
    {
        try
        {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(exePath);
            return AppInstallService.NormalizeVersion(info.ProductVersion)
                ?? AppInstallService.NormalizeVersion(info.FileVersion);
        }
        catch
        {
            return null;
        }
    }
}
