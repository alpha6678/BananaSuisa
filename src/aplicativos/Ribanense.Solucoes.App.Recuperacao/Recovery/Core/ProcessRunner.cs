using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Ribanense.Solucoes.App.Recuperacao.Recovery.Core;

/// <summary>Requisição de execução de um processo externo.</summary>
public sealed record ProcessRequest(
    string FileName,
    string Arguments,
    int TimeoutMinutes = 120,
    bool ShowWindow = false);

/// <summary>Resultado da execução de um processo externo.</summary>
public sealed record ProcessResult(int ExitCode, IReadOnlyList<string> Output)
{
    public bool Ok => ExitCode == 0;

    public string OutputText => string.Join(Environment.NewLine, Output);
}

public interface IProcessRunner
{
    /// <summary>
    /// Executa um processo capturando a saída linha a linha. Lança
    /// <see cref="OperationCanceledException"/> quando cancelado e
    /// <see cref="TimeoutException"/> quando excede o tempo limite.
    /// </summary>
    Task<ProcessResult> RunAsync(ProcessRequest request, IProgress<string>? output = null, CancellationToken ct = default);
}

/// <summary>
/// Executor de processos com captura de saída em tempo real, timeout (watchdog) e
/// cancelamento que encerra a árvore de processos — equivalente nativo do
/// Invoke-WithJob do script de referência.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(ProcessRequest request, IProgress<string>? output = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
            throw new ArgumentException("FileName obrigatório.", nameof(request));

        var psi = new ProcessStartInfo
        {
            FileName = request.FileName,
            Arguments = request.Arguments,
            UseShellExecute = false,
            CreateNoWindow = !request.ShowWindow,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };

        using var process = new Process { StartInfo = psi };
        var lines = new List<string>();
        var sync = new object();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (sync) lines.Add(e.Data);
            output?.Report(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            lock (sync) lines.Add(e.Data);
            output?.Report(e.Data);
        };

        ct.ThrowIfCancellationRequested();
        process.Start();
        try
        {
            using CancellationTokenRegistration _ = ct.Register(() =>
            {
                try { process.Kill(entireProcessTree: true); } catch { }
            });

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (request.TimeoutMinutes > 0)
                timeoutCts.CancelAfter(TimeSpan.FromMinutes(request.TimeoutMinutes));

            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Expirou o timeout interno: mata e converte para TimeoutException.
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException($"Processo '{request.FileName}' excedeu {request.TimeoutMinutes} min e foi encerrado.");
        }

        if (ct.IsCancellationRequested)
            throw new OperationCanceledException(ct);

        return new ProcessResult(process.ExitCode, lines);
    }
}
