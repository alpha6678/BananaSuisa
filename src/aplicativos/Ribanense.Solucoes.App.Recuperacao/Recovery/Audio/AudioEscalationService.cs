using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ribanense.Solucoes.App.Recuperacao.Recovery.Core;

namespace Ribanense.Solucoes.App.Recuperacao.Recovery.Audio;

public interface IAudioEscalationService
{
    /// <summary>Lista drivers MEDIA de terceiros no Driver Store (requer elevação).</summary>
    Task<IReadOnlyList<MediaDriverInfo>> ListMediaDriversAsync(IProgress<OperationEvent> events, CancellationToken ct);

    /// <summary>
    /// Garante backup de drivers: usa o export mais recente ou exporta agora.
    /// Retorna a pasta do export ou null quando impossível (aborta a escalada).
    /// </summary>
    Task<string?> EnsureDriverBackupAsync(
        string appDataRoot, RunContext run, IProgress<OperationEvent> events, CancellationToken ct);

    /// <summary>Remove os drivers escolhidos (pnputil /delete-driver /uninstall /force) e revarre o hardware.</summary>
    Task<IReadOnlyList<RemocaoDriverResultado>> RemoveDriversAsync(
        IReadOnlyList<MediaDriverInfo> drivers,
        RunContext run,
        IConfirmDialog confirm,
        IProgress<OperationEvent> events,
        CancellationToken ct);
}

/// <summary>
/// Escalada (destructiva, passo a passo com confirmação): remoção cirúrgica de
/// drivers de áudio com backup obrigatório e orientação de reparo in-place —
/// porta da Fase 4 do script de referência.
/// </summary>
public sealed class AudioEscalationService(IProcessRunner runner) : IAudioEscalationService
{
    private readonly PowerShellProbe probe = new(runner);

    internal static string BuildMediaDriversScript() => """
        Get-WindowsDriver -Online -All -ErrorAction SilentlyContinue |
            Where-Object { $_.ClassName -eq 'MEDIA' } |
            Select-Object Driver,ProviderName,Version,Date | ConvertTo-Json -Depth 2 -Compress
        """;

    private sealed record DriverRow(string? Driver, string? ProviderName, string? Version, string? Date);

    public async Task<IReadOnlyList<MediaDriverInfo>> ListMediaDriversAsync(
        IProgress<OperationEvent> events, CancellationToken ct)
    {
        List<DriverRow> rows = await probe.QueryJsonListAsync<DriverRow>(
            BuildMediaDriversScript(), ct: ct, timeoutMinutes: 15).ConfigureAwait(false);

        var drivers = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Driver))
            .Select(r => new MediaDriverInfo(r.Driver!, r.ProviderName ?? "?", r.Version ?? "?", PowerShellProbe.NormalizePsDate(r.Date)))
            .ToList();

        if (drivers.Count == 0)
        {
            events.Report(new OperationEvent(OperationEventKind.Ok,
                "Nenhum driver MEDIA de terceiros para remover."));
        }
        return drivers;
    }

    public async Task<string?> EnsureDriverBackupAsync(
        string appDataRoot, RunContext run, IProgress<OperationEvent> events, CancellationToken ct)
    {
        // 1) Export desta execução.
        string destaExecucao = Path.Combine(run.Root, "backup", "drivers-export");
        if (Directory.Exists(destaExecucao) && Directory.EnumerateFileSystemEntries(destaExecucao).Any())
            return destaExecucao;

        // 2) Export de execução anterior.
        string? anterior = AudioBackupService.FindDriverExportUnder(appDataRoot);
        if (anterior is not null)
        {
            events.Report(new OperationEvent(OperationEventKind.Ok, $"Usando drivers já exportados em: {anterior}"));
            return anterior;
        }

        // 3) Exportar agora — sem backup não há remoção (sem volta se apagar sem backup).
        events.Report(new OperationEvent(OperationEventKind.Warning,
            "Não há backup de drivers. Exportando antes de qualquer remoção..."));
        var backupService = new AudioBackupService(runner);
        BackupItem item = await backupService.ExportarDriversParaEscaladaAsync(run, events, ct).ConfigureAwait(false);
        if (item.Status == BackupItemStatus.Ok) return ExtrairDiretorio(item.Detalhe);

        events.Report(new OperationEvent(OperationEventKind.Error,
            "Export falhou. ABORTANDO remoção de drivers (sem volta se apagar sem backup)."));
        return null;
    }

    internal static string ExtrairDiretorio(string detalhe)
    {
        int pos = detalhe.LastIndexOf(" em ", StringComparison.Ordinal);
        return pos >= 0 ? detalhe[(pos + 4)..].Trim() : detalhe;
    }

    public async Task<IReadOnlyList<RemocaoDriverResultado>> RemoveDriversAsync(
        IReadOnlyList<MediaDriverInfo> drivers,
        RunContext run,
        IConfirmDialog confirm,
        IProgress<OperationEvent> events,
        CancellationToken ct)
    {
        var resultados = new List<RemocaoDriverResultado>();
        foreach (MediaDriverInfo d in drivers)
        {
            ct.ThrowIfCancellationRequested();
            events.Report(new OperationEvent(OperationEventKind.Info, $"Removendo {d.OemInf} ({d.Provider})..."));
            try
            {
                ProcessResult r = await runner.RunAsync(new ProcessRequest(
                    "pnputil.exe", $"/delete-driver {d.OemInf} /uninstall /force", TimeoutMinutes: 15), null, ct).ConfigureAwait(false);
                resultados.Add(r.Ok
                    ? new RemocaoDriverResultado(d.OemInf, true, "removido")
                    : new RemocaoDriverResultado(d.OemInf, false, $"pnputil exit={r.ExitCode}"));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                resultados.Add(new RemocaoDriverResultado(d.OemInf, false, ex.Message));
            }
        }

        events.Report(new OperationEvent(OperationEventKind.Info, "Revarrendo hardware após remoções..."));
        try
        {
            await runner.RunAsync(new ProcessRequest("pnputil.exe", "/scan-devices", TimeoutMinutes: 10), null, ct).ConfigureAwait(false);
        }
        catch { }

        File.WriteAllLines(run.FilePath("escalada-remocoes.txt"),
            resultados.Select(r => $"{r.OemInf} | {(r.Ok ? "OK" : "FALHOU")} | {r.Detalhe}"));
        return resultados;
    }

    /// <summary>Guia do reparo in-place (a cura definitiva para Windows modificado).</summary>
    public const string GuiaInPlace = """
        1. Baixe a ISO oficial do Windows 11 (mesma edição/idioma):
           https://www.microsoft.com/pt-br/software-download/windows11
        2. Monte a ISO (duplo clique) e execute setup.exe.
        3. Em "Escolha o que manter": marque "Manter arquivos e aplicativos pessoais".
        4. Siga o assistente. O Windows será regravado por cima, preservando tudo.
        5. Se a instalação atual for uma build enxutada (NTLite/tiny11/Atlas),
           a ISO oficial substitui os componentes faltantes — é exatamente o que
           corrige "alguém modificou o Windows" sem formatar.
        """;
}
