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
    /// <summary>URL crua do MAS AIO no GitHub (massgravel/MAS).</summary>
    public const string MasAioUrl =
        "https://raw.githubusercontent.com/massgravel/MAS/master/MAS/All-In-One-Version/MAS_AIO.cmd";

    private readonly string _cacheDir;
    private readonly IElevatedCommandRunner _elevated;
    private readonly Func<HttpClient> _httpClientFactory;

    public MasRunner(string cacheDir, IElevatedCommandRunner elevated, Func<HttpClient>? httpClientFactory = null)
    {
        _cacheDir = cacheDir ?? throw new ArgumentNullException(nameof(cacheDir));
        _elevated = elevated ?? throw new ArgumentNullException(nameof(elevated));
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

        // O MAS_AIO.cmd e interativo; passamos o codigo do metodo como argumento
        // para saltar o menu. Se a versao do MAS nao aceitar, ele abre o menu.
        string script = $"& cmd /c '\"{scriptPath}\" {method.MenuCode}'";

        ElevatedResult result = await _elevated.RunScriptAsync(script, onLine, ct).ConfigureAwait(false);

        if (result.Cancelled)
        {
            return new MasRunResult(false, "Elevação cancelada (UAC).", true);
        }

        if (result.ExitCode == ElevatedCommandRunner.UacCancelledExitCode)
        {
            return new MasRunResult(false, "Elevação cancelada (UAC).", true);
        }

        if (result.ExitCode != 0)
        {
            return new MasRunResult(false, $"MAS encerrou com código {result.ExitCode}.", false);
        }

        return new MasRunResult(true, null, false);
    }
}
