using System.Diagnostics;
using System.IO;
using Ribanense.Solucoes.Launcher.Configuration;
using Ribanense.Solucoes.PluginSDK;
using Ribanense.Solucoes.PluginSDK.Payload;

namespace Ribanense.Solucoes.Launcher.Services;

public sealed record LauncherDeploymentResult(bool Success, string? Error, bool ShutdownRequested);

/// <summary>
/// Instala esta cópia em %LOCALAPPDATA%\Programs, gera uma cópia portátil
/// ou remove o registro e os atalhos. O processo novo sobe com --post-update
/// para esperar o mutex desta instância.
/// </summary>
public sealed class LauncherSelfInstaller
{
    private readonly InstallRegistrationService _registration;

    public LauncherSelfInstaller(IInstallRegistration? registration = null)
    {
        _registration = new InstallRegistrationService(registration ?? new WindowsInstallRegistration());
    }

    public LauncherDeploymentResult InstallHere()
    {
        string? exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
            return new LauncherDeploymentResult(false, "Executável atual não encontrado.", false);

        string sourceDir = Path.GetDirectoryName(exe)!;
        string destination = DeploymentLayout.DefaultInstallDirectory;
        if (!PathsEqual(sourceDir, destination))
        {
            try
            {
                if (Directory.Exists(destination))
                    Directory.Delete(destination, recursive: true);
                PayloadApplier.CopyDeployment(sourceDir, destination);
            }
            catch (Exception ex)
            {
                return new LauncherDeploymentResult(false, $"Falha ao copiar o launcher: {ex.Message}", false);
            }
        }

        string entry = ResolveEntry(destination, exe);
        if (!File.Exists(entry))
            return new LauncherDeploymentResult(false, "A cópia instalada não tem executável.", false);

        try
        {
            _registration.RegisterLauncher(destination, entry, AppVersion.ForEntry());
        }
        catch (Exception ex)
        {
            return new LauncherDeploymentResult(false, $"Falha ao registrar a instalação: {ex.Message}", false);
        }

        if (PathsEqual(sourceDir, destination))
            return new LauncherDeploymentResult(true, null, false);

        if (!StartPostUpdate(entry, Environment.ProcessId))
            return new LauncherDeploymentResult(false, "A cópia foi instalada, mas não abriu.", false);

        return new LauncherDeploymentResult(true, null, true);
    }

    public LauncherDeploymentResult MakePortable(string destination)
    {
        if (string.IsNullOrWhiteSpace(destination))
            return new LauncherDeploymentResult(false, "Escolha uma pasta para a cópia portátil.", false);

        string? exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
            return new LauncherDeploymentResult(false, "Executável atual não encontrado.", false);

        string sourceDir = Path.GetDirectoryName(exe)!;
        if (PathsEqual(sourceDir, destination))
        {
            File.WriteAllText(Path.Combine(destination, PayloadManifest.PortableMarkerFileName), "1");
            if (!StartPostUpdate(exe, Environment.ProcessId))
                return new LauncherDeploymentResult(false, "A pasta foi marcada como portátil, mas a cópia não reabriu.", false);
            return new LauncherDeploymentResult(true, null, true);
        }

        try
        {
            Directory.CreateDirectory(destination);
            PayloadApplier.CopyDeployment(sourceDir, destination);
            File.WriteAllText(Path.Combine(destination, PayloadManifest.PortableMarkerFileName), "1");
            CopyIfMissing(
                LauncherConfig.LauncherDataRoot,
                Path.Combine(destination, "dados"));
            CopyIfMissing(
                LauncherConfig.AplicativosRoot,
                Path.Combine(destination, "aplicativos"));
        }
        catch (Exception ex)
        {
            return new LauncherDeploymentResult(false, $"Falha ao preparar a cópia portátil: {ex.Message}", false);
        }

        string entry = ResolveEntry(destination, exe);
        if (!File.Exists(entry) || !StartPostUpdate(entry, Environment.ProcessId))
            return new LauncherDeploymentResult(false, "A pasta portátil foi criada, mas a cópia não abriu.", false);

        return new LauncherDeploymentResult(true, null, true);
    }

    public LauncherDeploymentResult Uninstall()
    {
        DeploymentContext deployment = LauncherConfig.Deployment;
        try
        {
            _registration.UnregisterLauncher();
        }
        catch
        {
            // o apagamento dos arquivos ainda segue
        }

        if (deployment.Mode == DeploymentMode.Installed)
        {
            ScheduleDelete(deployment.InstallDirectory);
            return new LauncherDeploymentResult(true, null, true);
        }

        if (deployment.Mode == DeploymentMode.Portable)
        {
            try
            {
                string flag = Path.Combine(deployment.InstallDirectory, PayloadManifest.PortableMarkerFileName);
                if (File.Exists(flag)) File.Delete(flag);
            }
            catch (Exception ex)
            {
                return new LauncherDeploymentResult(false, ex.Message, false);
            }

            return new LauncherDeploymentResult(true, null, true);
        }

        return new LauncherDeploymentResult(false,
            "Esta cópia não está instalada nem marcada como portátil.", false);
    }

    private static string ResolveEntry(string directory, string currentExe)
    {
        string manifestPath = Path.Combine(directory, PayloadManifest.FileName);
        if (File.Exists(manifestPath))
        {
            try
            {
                string entry = PayloadManifest.Load(manifestPath).EntryExecutable;
                if (!string.IsNullOrWhiteSpace(entry))
                {
                    string path = Path.Combine(directory, entry.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(path)) return path;
                }
            }
            catch
            {
                // usa o nome do processo atual
            }
        }

        string sameName = Path.Combine(directory, Path.GetFileName(currentExe));
        if (File.Exists(sameName)) return sameName;
        string stable = Path.Combine(directory, DeploymentLayout.LauncherExecutableName);
        return File.Exists(stable) ? stable : sameName;
    }

    private static bool StartPostUpdate(string exe, int pid)
    {
        try
        {
            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? Environment.CurrentDirectory
            };
            psi.ArgumentList.Add(LauncherUpdateService.PostUpdateArg);
            psi.ArgumentList.Add(pid.ToString());
            return Process.Start(psi) is not null;
        }
        catch
        {
            return false;
        }
    }

    private static void CopyIfMissing(string source, string destination)
    {
        if (!Directory.Exists(source) || Directory.Exists(destination)) return;
        PayloadApplier.CopyTree(source, destination);
    }

    private static void ScheduleDelete(string directory)
    {
        string script = Path.Combine(Path.GetTempPath(), "ribanense-uninstall-" + Guid.NewGuid().ToString("N") + ".cmd");
        string quoted = directory.Replace("\"", string.Empty);
        File.WriteAllText(script,
            "@echo off\r\n" +
            "timeout /t 2 /nobreak >nul\r\n" +
            $"rmdir /s /q \"{quoted}\"\r\n" +
            "del \"%~f0\"\r\n");
        Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + script + "\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
}
