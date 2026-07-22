using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace Ribanense.Solucoes.App.Sistema.Services;

/// <summary>
/// Baixa o MAS AIO (Microsoft Activation Scripts) para o cache do app e o
/// executa elevado, passando os argumentos do metodo escolhido. Wrapper
/// open-source do MAS.
/// </summary>
public sealed class MasRunner : IMasRunner
{
    /// <summary>URL crua do MAS AIO (massgravel/Microsoft-Activation-Scripts).</summary>
    public const string MasAioUrl =
        "https://raw.githubusercontent.com/massgravel/Microsoft-Activation-Scripts/05c4f881efec946c0040cdd552d1afa9a519704b/MAS/All-In-One-Version-KL/MAS_AIO.cmd";

    private const int UacCancelledExitCode = 1223;

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

        // MAS_AIO.cmd e interativo: janela visivel para o usuario acompanhar/confirmar.
        // Argumento vazio (Troubleshoot) abre o menu principal do MAS.
        string innerCommand = string.IsNullOrWhiteSpace(method.Arguments)
            ? $"\"{scriptPath}\""
            : $"\"{scriptPath}\" {method.Arguments}";

        var psi = new ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Normal,
            CreateNoWindow = false,
        };
        psi.ArgumentList.Add("/c");
        psi.ArgumentList.Add(innerCommand);

        int exitCode;
        try
        {
            onLine?.Report("Abrindo janela elevada do MAS (confirme no UAC)...");
            exitCode = await _launcher.StartAndWaitAsync(psi, ct).ConfigureAwait(false);
        }
        catch (System.ComponentModel.Win32Exception win32) when (win32.NativeErrorCode == UacCancelledExitCode)
        {
            return new MasRunResult(false, "Elevação cancelada (UAC).", true);
        }
        catch (OperationCanceledException)
        {
            return new MasRunResult(false, "Operação cancelada.", true);
        }

        // Os codigos de saida do MAS nao sao um contrato estavel de sucesso/erro;
        // o resultado real e mostrado na janela do MAS. Reportamos o encerramento
        // e deixamos o usuario conferir por la.
        if (exitCode != 0)
        {
            onLine?.Report($"Janela do MAS encerrada (código {exitCode}). Confira o resultado na janela do MAS.");
        }

        return new MasRunResult(true, null, false);
    }
}
