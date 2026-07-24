using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace Ribanense.Solucoes.App.Sistema.Services;

/// <summary>
/// Baixa e executa o MAS AIO (Microsoft Activation Scripts) para ativação do Windows e Office,
/// suportando modo de terminal interativo, execução direta, alternância entre CMD/PowerShell
/// e atualização sob demanda do script de terceiros.
/// </summary>
public sealed class MasRunner : IMasRunner
{
    /// <summary>URL principal do MAS AIO no repositório oficial (massgravel).</summary>
    public const string MasAioUrl =
        "https://raw.githubusercontent.com/massgravel/Microsoft-Activation-Scripts/master/MAS/All-In-One-Version-KL/MAS_AIO.cmd";

    /// <summary>URL de fallback do MAS AIO em commit conhecido.</summary>
    public const string MasAioFallbackUrl =
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

    public MasScriptInfo GetScriptInfo()
    {
        string scriptPath = Path.Combine(_cacheDir, "MAS_AIO.cmd");
        if (!File.Exists(scriptPath))
        {
            return new MasScriptInfo(false, null, scriptPath);
        }

        var fi = new FileInfo(scriptPath);
        return new MasScriptInfo(true, fi.LastWriteTime, scriptPath);
    }

    public async Task<bool> RedownloadScriptAsync(IProgress<string>? onLine, CancellationToken ct)
    {
        Directory.CreateDirectory(_cacheDir);
        string scriptPath = Path.Combine(_cacheDir, "MAS_AIO.cmd");
        onLine?.Report("Solicitando atualização do script MAS AIO...");

        bool downloaded = await TryDownloadScriptAsync(scriptPath, onLine, ct).ConfigureAwait(false);
        if (downloaded)
        {
            onLine?.Report($"MAS AIO atualizado e salvo em {scriptPath}.");
        }
        else
        {
            onLine?.Report("Não foi possível atualizar o MAS AIO das fontes online.");
        }

        return downloaded;
    }

    public Task<MasRunResult> RunAsync(MasMethod method, IProgress<string>? onLine, CancellationToken ct)
    {
        return RunAsync(method, new MasRunOptions(), onLine, ct);
    }

    public async Task<MasRunResult> RunAsync(MasMethod method, MasRunOptions? options, IProgress<string>? onLine, CancellationToken ct)
    {
        if (method is null) throw new ArgumentNullException(nameof(method));

        options ??= new MasRunOptions();
        Directory.CreateDirectory(_cacheDir);
        string scriptPath = Path.Combine(_cacheDir, "MAS_AIO.cmd");

        if (options.ForceRedownload || !File.Exists(scriptPath))
        {
            onLine?.Report("Script MAS AIO ausente ou atualização solicitada. Garantindo versão local...");
            bool downloaded = await TryDownloadScriptAsync(scriptPath, onLine, ct).ConfigureAwait(false);
            if (!downloaded && !File.Exists(scriptPath) && options.Engine != MasEngine.PowerShell)
            {
                return new MasRunResult(false, "Falha ao obter o script MAS AIO e nenhum arquivo local está disponível.", false);
            }
        }

        ProcessStartInfo psi = BuildProcessStartInfo(scriptPath, method, options, onLine);

        int exitCode;
        try
        {
            string engineLabel = options.Engine == MasEngine.PowerShell ? "PowerShell" : "CMD";
            string modeLabel = options.InteractiveTerminal ? "Terminal Interativo (/k)" : "Execução Direta (/c)";
            onLine?.Report($"Iniciando janela elevada [{engineLabel} — {modeLabel}] (confirme o prompt do UAC)...");

            if (options.InteractiveTerminal)
            {
                onLine?.Report("Interatividade ativa: a janela do terminal permanecerá aberta para que você possa digitar opções, responder perguntas e visualizar o progresso.");
            }

            exitCode = await _launcher.StartAndWaitAsync(psi, ct).ConfigureAwait(false);
        }
        catch (System.ComponentModel.Win32Exception win32) when (win32.NativeErrorCode == UacCancelledExitCode)
        {
            return new MasRunResult(false, "Elevação cancelada pelo usuário (UAC).", true);
        }
        catch (OperationCanceledException)
        {
            return new MasRunResult(false, "Operação cancelada.", true);
        }
        catch (Exception ex)
        {
            return new MasRunResult(false, $"Erro ao iniciar processo do MAS: {ex.Message}", false);
        }

        if (exitCode != 0)
        {
            onLine?.Report($"Janela do MAS encerrada (código de saída {exitCode}). Confira os detalhes na janela do terminal.");
        }
        else
        {
            onLine?.Report("Janela do MAS encerrada com código 0.");
        }

        return new MasRunResult(true, null, false);
    }

    private ProcessStartInfo BuildProcessStartInfo(string scriptPath, MasMethod method, MasRunOptions options, IProgress<string>? onLine)
    {
        bool fileExists = File.Exists(scriptPath);

        if (options.Engine == MasEngine.PowerShell)
        {
            var psi = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Normal,
                CreateNoWindow = false,
            };

            if (options.InteractiveTerminal)
            {
                psi.ArgumentList.Add("-NoExit");
            }

            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-ExecutionPolicy");
            psi.ArgumentList.Add("Bypass");
            psi.ArgumentList.Add("-Command");

            if (fileExists)
            {
                string psCommand = string.IsNullOrWhiteSpace(method.Arguments)
                    ? $"& {{ & '{scriptPath}' }}"
                    : $"& {{ & '{scriptPath}' {method.Arguments} }}";
                psi.ArgumentList.Add(psCommand);
            }
            else
            {
                onLine?.Report("Script local ausente. Executando via PowerShell online (get.activated.win)...");
                psi.ArgumentList.Add("iwr -useb https://get.activated.win | iex");
            }

            return psi;
        }
        else
        {
            string switchParam = options.InteractiveTerminal ? "/k" : "/c";
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

            psi.ArgumentList.Add(switchParam);
            psi.ArgumentList.Add(innerCommand);
            return psi;
        }
    }

    private async Task<bool> TryDownloadScriptAsync(string scriptPath, IProgress<string>? onLine, CancellationToken ct)
    {
        using HttpClient http = _httpClientFactory();

        try
        {
            onLine?.Report($"Baixando do repositório oficial MAS ({MasAioUrl})...");
            byte[] bytes = await http.GetByteArrayAsync(MasAioUrl, ct).ConfigureAwait(false);
            if (bytes.Length > 0)
            {
                await File.WriteAllBytesAsync(scriptPath, bytes, ct).ConfigureAwait(false);
                return true;
            }
        }
        catch (Exception ex)
        {
            onLine?.Report($"Aviso: Falha ao baixar da URL principal ({ex.Message}). Tentando fonte de fallback...");
        }

        try
        {
            onLine?.Report($"Baixando da URL de fallback ({MasAioFallbackUrl})...");
            byte[] bytes = await http.GetByteArrayAsync(MasAioFallbackUrl, ct).ConfigureAwait(false);
            if (bytes.Length > 0)
            {
                await File.WriteAllBytesAsync(scriptPath, bytes, ct).ConfigureAwait(false);
                return true;
            }
        }
        catch (Exception ex)
        {
            onLine?.Report($"Erro ao baixar das fontes conhecidas do MAS: {ex.Message}");
        }

        return false;
    }
}
