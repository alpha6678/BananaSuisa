using System.IO;
using System.Net.Http;

namespace Ribanense.Solucoes.Launcher.Services;

/// <summary>
/// Instala o .NET Desktop Runtime pelo bootstrapper oficial da Microsoft
/// (<c>https://aka.ms/dotnet/{major}.{minor}/windowsdesktop-runtime-win-x64.exe</c>),
/// sem abrir o navegador. A instalacao usa /passive: a Microsoft mostra a janela
/// de progresso. O Windows pede permissao de administrador (UAC).
/// </summary>
public sealed class DotNetDesktopRuntimeInstaller : IDotNetDesktopRuntimeInstaller
{
    private const int ErrorCancelled = 1223;

    private readonly HttpClient _http;

    public DotNetDesktopRuntimeInstaller()
        : this(CreateClient(), ownsHttp: true)
    {
    }

    internal DotNetDesktopRuntimeInstaller(HttpClient http, bool ownsHttp = false)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _ownsHttp = ownsHttp;
    }

    private readonly bool _ownsHttp;

    public static string? BuildInstallerUrl(string requiredVersion)
    {
        if (!Version.TryParse(requiredVersion, out var version)) return null;
        return $"https://aka.ms/dotnet/{version.Major}.{version.Minor}/windowsdesktop-runtime-win-x64.exe";
    }

    public static RuntimeInstallKind InterpretExitCode(int exitCode) => exitCode switch
    {
        0 => RuntimeInstallKind.Success,
        1641 or 3010 => RuntimeInstallKind.RebootRequired,
        1602 => RuntimeInstallKind.Cancelled,
        _ => RuntimeInstallKind.Failed
    };

    public static string DependencyList(string requiredVersion)
    {
        string label = VersionLabel(requiredVersion);
        return $".NET Runtime {label} (x64)\n.NET Desktop Runtime {label} (x64)";
    }

    public async Task<RuntimeInstallResult> InstallAsync(
        string requiredVersion,
        IProgress<RuntimeInstallProgress>? progress,
        CancellationToken ct)
    {
        string? url = BuildInstallerUrl(requiredVersion);
        if (url is null)
        {
            return new RuntimeInstallResult(RuntimeInstallKind.Failed,
                $"Versão de runtime inválida: {requiredVersion}.");
        }

        string dependencies = DependencyList(requiredVersion);
        string dir = Path.Combine(Path.GetTempPath(), "Ribanense");
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, $"windowsdesktop-runtime-{VersionLabel(requiredVersion)}-win-x64.exe");

        progress?.Report(new RuntimeInstallProgress($"Baixando:\n{dependencies}", 0));

        try
        {
            await DownloadAsync(url, file, progress, dependencies, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            return new RuntimeInstallResult(RuntimeInstallKind.Failed,
                $"Não foi possível baixar o .NET Desktop Runtime. {ex.Message}");
        }

        progress?.Report(new RuntimeInstallProgress($"Instalando:\n{dependencies}", null));

        try
        {
            int code = await RunElevatedAsync(file, ct).ConfigureAwait(false);
            var kind = InterpretExitCode(code);
            string? detail = kind == RuntimeInstallKind.Failed
                ? $"O instalador do .NET terminou com o código {code}."
                : null;
            return new RuntimeInstallResult(kind, detail);
        }
        catch (OperationCanceledException)
        {
            return new RuntimeInstallResult(RuntimeInstallKind.Cancelled, null);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            return new RuntimeInstallResult(RuntimeInstallKind.Cancelled, null);
        }
        catch (Exception ex)
        {
            return new RuntimeInstallResult(RuntimeInstallKind.Failed, ex.Message);
        }
    }

    private static string VersionLabel(string requiredVersion)
    {
        if (!Version.TryParse(requiredVersion, out var version)) return "runtime";
        return $"{version.Major}.{version.Minor}";
    }

    private async Task DownloadAsync(
        string url,
        string destination,
        IProgress<RuntimeInstallProgress>? progress,
        string dependencies,
        CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        long? total = response.Content.Headers.ContentLength;
        await using var input = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);

        byte[] buffer = new byte[81920];
        long read = 0;
        int n;
        while ((n = await input.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
            read += n;
            if (total.HasValue && total.Value > 0)
            {
                progress?.Report(new RuntimeInstallProgress(
                    $"Baixando:\n{dependencies}",
                    (double)read / total.Value * 100.0));
            }
        }
    }

    private static async Task<int> RunElevatedAsync(string fileName, CancellationToken ct)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = fileName,
            Arguments = "/install /passive /norestart",
            UseShellExecute = true,
            Verb = "runas"
        };

        using var process = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException("Não foi possível iniciar o instalador do .NET.");
        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        return process.ExitCode;
    }

    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("RibanenseSolucoes-Launcher/1.0");
        return http;
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }
}
