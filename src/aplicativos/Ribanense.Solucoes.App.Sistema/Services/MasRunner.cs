using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace Ribanense.Solucoes.App.Sistema.Services;

/// <summary>
/// Baixa o MAS AIO (Microsoft Activation Scripts) para o cache do app e o
/// executa elevado, passando o metodo escolhido. Wrapper open-source do MAS.
/// </summary>
public sealed class MasRunner : IMasRunner
{
    /// <summary>URL crua do MAS AIO (massgravel/Microsoft-Activation-Scripts).</summary>
    public const string MasAioUrl =
        "https://raw.githubusercontent.com/massgravel/Microsoft-Activation-Scripts/05c4f881efec946c0040cdd552d1afa9a519704b/MAS/All-In-One-Version-KL/MAS_AIO.cmd";

    private readonly string _cacheDir;
    private readonly IProcessLauncher _launcher;
    private readonly Func<HttpClient> _httpClientFactory;

    public MasRunner(string cacheDir, IProcessLauncher launcher, Func<HttpClient>? httpClientFactory = null)
    {
        _cacheDir = cacheDir ?? throw new ArgumentNullException(nameof(cacheDir));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _httpClientFactory = httpClientFactory ?? (() => new HttpClient { Timeout = TimeSpan.FromMinutes(5) });
    }

    public async Task<MasRunResult> RunAsync(MasMethod method, IProgress<string>? onLine, CancellationToken ct)
    {
        if (method is null) throw new ArgumentNullException(nameof(method));

        Directory.CreateDirectory(_cacheDir);
        string scriptPath = Path.Combine(_cacheDir, "MAS_AIO.cmd");

        try
        {
            if (!File.Exists(scriptPath))
            {
                onLine?.Report("Baixando MAS AIO...");
                using HttpClient http = _httpClientFactory();
                byte[] bytes = await http.GetByteArrayAsync(MasAioUrl, ct).ConfigureAwait(false);
                await File.WriteAllBytesAsync(scriptPath, bytes, ct).ConfigureAwait(false);
                onLine?.Report($"MAS AIO salvo em {scriptPath}.");
            }
        }
        catch (OperationCanceledException)
        {
            return new MasRunResult(false, "Operação cancelada.", true);
        }
        catch (Exception ex)
        {
            return new MasRunResult(false, $"Falha ao baixar o MAS: {ex.Message}", false);
        }

        // MAS_AIO.cmd e interativo: precisa de janela visivel para o usuario
        // escolher/confirmar o metodo. Passamos o codigo do metodo como argumento
        // para saltar o menu quando a versao do MAS aceitar.
        var psi = new ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Normal,
            CreateNoWindow = false,
        };
        psi.ArgumentList.Add("/c");
        psi.ArgumentList.Add($"\"{scriptPath}\" {method.MenuCode}");

        int exitCode;
        try
        {
            onLine?.Report("Abrindo janela elevada do MAS...");
            exitCode = await _launcher.StartAndWaitAsync(psi, ct).ConfigureAwait(false);
        }
        catch (System.ComponentModel.Win32Exception win32) when (win32.NativeErrorCode == 1223)
        {
            return new MasRunResult(false, "Elevação cancelada (UAC).", true);
        }
        catch (OperationCanceledException)
        {
            return new MasRunResult(false, "Operação cancelada.", true);
        }

        if (exitCode != 0)
        {
            return new MasRunResult(false, $"MAS encerrou com código {exitCode}.", false);
        }

        return new MasRunResult(true, null, false);
    }
}
