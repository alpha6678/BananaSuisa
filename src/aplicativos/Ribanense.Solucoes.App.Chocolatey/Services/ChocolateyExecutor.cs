using System.Diagnostics;
using System.Text;
using Ribanense.Solucoes.App.Chocolatey.Domain;

namespace Ribanense.Solucoes.App.Chocolatey.Services;

public sealed class ChocolateyExecutor : IChocolateyExecutor
{
    private readonly IChocolateyLocator _locator;

    public ChocolateyExecutor(IChocolateyLocator locator)
    {
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
    }

    public async Task<ChocolateyRunResult> RunAsync(
        IEnumerable<string> args,
        Action<string>? onStdout = null,
        Action<string>? onStderr = null,
        bool requireAdmin = false,
        CancellationToken ct = default)
    {
        string? exe = _locator.TryLocate();
        if (exe is null)
        {
            throw new InvalidOperationException(
                "choco.exe não encontrado. Instale o Chocolatey ou verifique se ele está no PATH.");
        }

        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = !requireAdmin,
            RedirectStandardError = !requireAdmin,
            UseShellExecute = requireAdmin,
            CreateNoWindow = !requireAdmin,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        if (requireAdmin)
        {
            psi.Verb = "runas";
        }

        foreach (string a in args) psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        if (!requireAdmin)
        {
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null) return;
                stdout.AppendLine(e.Data);
                onStdout?.Invoke(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is null) return;
                stderr.AppendLine(e.Data);
                onStderr?.Invoke(e.Data);
            };
        }

        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception ex) when (requireAdmin && ex.NativeErrorCode == 1223)
        {
            // O usuário cancelou o prompt do UAC
            return new ChocolateyRunResult(-1, "", "Operação cancelada pelo usuário (UAC).");
        }

        if (!requireAdmin)
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            throw;
        }

        if (requireAdmin)
        {
            return new ChocolateyRunResult(process.ExitCode, "Executado como administrador (logs via console).", "");
        }

        return new ChocolateyRunResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
