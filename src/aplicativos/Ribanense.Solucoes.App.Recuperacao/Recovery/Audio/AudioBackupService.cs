using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Ribanense.Solucoes.App.Recuperacao.Recovery.Core;

namespace Ribanense.Solucoes.App.Recuperacao.Recovery.Audio;

/// <summary>Ponto de decisão da UI (confirmar ações em diálogos).</summary>
public interface IConfirmDialog
{
    /// <summary>Exibe pergunta de confirmação. true = operador aceitou.</summary>
    Task<bool> ConfirmAsync(string titulo, string mensagem);
}

public interface IAudioBackupService
{
    /// <summary>Executa a fase 2 (ponto de restauração + drivers + registro).</summary>
    Task<BackupResultado> RunAsync(
        RunContext run,
        IConfirmDialog confirm,
        IProgress<OperationEvent> events,
        CancellationToken ct);

    /// <summary>Procura o export de drivers mais recente (desta ou de execuções anteriores).</summary>
    Task<string?> FindExistingDriverExportAsync(string appDataRoot, CancellationToken ct);
}

/// <summary>
/// Backup pré-reparo: ponto de restauração (com conferência real), export de
/// drivers via DISM e export das chaves de registro críticas, gravando o
/// backup-manifest.json que trava o Reparo — porta da Fase 2 do script.
/// </summary>
public sealed class AudioBackupService(IProcessRunner runner) : IAudioBackupService
{
    private static readonly (string SubKey, string Arquivo)[] ChavesExport =
    [
        (@"HKLM\SYSTEM\CurrentControlSet\Services\Audiosrv", "servico-Audiosrv.reg"),
        (@"HKLM\SYSTEM\CurrentControlSet\Services\AudioEndpointBuilder", "servico-AudioEndpointBuilder.reg"),
        (@"HKLM\SYSTEM\CurrentControlSet\Services\HDAudBus", "servico-HDAudBus.reg"),
        (@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices", "MMDevices.reg"),
        (@"HKLM\SOFTWARE\Policies\Microsoft\Windows\DeviceInstall", "DeviceInstall-Policies.reg"),
        (@"HKLM\SYSTEM\CurrentControlSet\Control\Terminal Server\WinStations", "WinStations.reg"),
    ];

    public async Task<BackupResultado> RunAsync(
        RunContext run,
        IConfirmDialog confirm,
        IProgress<OperationEvent> events,
        CancellationToken ct)
    {
        events.Report(new OperationEvent(OperationEventKind.Info, "FASE 2 — BACKUP."));
        var itens = new List<BackupItem>();

        string pastaBackup = Path.Combine(run.Root, "backup");
        try
        {
            Directory.CreateDirectory(pastaBackup);
        }
        catch (Exception ex)
        {
            events.Report(new OperationEvent(OperationEventKind.Error,
                $"Não foi possível criar a pasta de backup ({ex.Message}). BACKUP ABORTADO."));
            return new BackupResultado(BackupStatusGeral.Incompleto,
                [new BackupItem("Pasta de backup", BackupItemStatus.Falhou, ex.Message)],
                pastaBackup, null, null);
        }

        // Espaço em disco: o export de drivers pode passar de 1 GB.
        var drive = new DriveInfo(Path.GetPathRoot(run.Root)!);
        if (drive.AvailableFreeSpace < 2L * 1024 * 1024 * 1024)
        {
            events.Report(new OperationEvent(OperationEventKind.Warning,
                $"Pouco espaço livre ({drive.AvailableFreeSpace / 1024 / 1024 / 1024} GB). O export de drivers pode falhar."));
        }

        await PrepararRestauracaoSistemaAsync(confirm, events, ct).ConfigureAwait(false);
        itens.Add(await CriarPontoRestauracaoAsync(run, confirm, events, ct).ConfigureAwait(false));
        itens.Add(await ExportarDriversAsync(pastaBackup, events, ct).ConfigureAwait(false));
        itens.AddRange(await ExportarRegistroAsync(pastaBackup, events).ConfigureAwait(false));

        var falhas = itens.Where(i => i.Status == BackupItemStatus.Falhou).ToList();
        var status = falhas.Count == 0 ? BackupStatusGeral.Completo : BackupStatusGeral.Incompleto;

        string? caminhoManifesto = null;
        try
        {
            var manifesto = new BackupManifest(
                run.RunStamp,
                run.Root,
                status == BackupStatusGeral.Completo ? "Completo" : "Incompleto",
                itens.Select(i => new BackupManifestItem(i.Nome, StatusTexto(i.Status), i.Detalhe)).ToList());
            caminhoManifesto = run.FilePath("backup-manifest.json");
            File.WriteAllText(caminhoManifesto,
                JsonSerializer.Serialize(manifesto, BackupManifest.JsonOptions));
            events.Report(new OperationEvent(OperationEventKind.Ok, $"Manifesto gravado em: {caminhoManifesto}"));
        }
        catch (Exception ex)
        {
            events.Report(new OperationEvent(OperationEventKind.Warning,
                $"Falha ao gravar o manifesto: {ex.Message}"));
        }

        if (status == BackupStatusGeral.Incompleto)
        {
            events.Report(new OperationEvent(OperationEventKind.Error,
                "BACKUP INCOMPLETO — resolva os itens [Falhou] antes do Reparo seguro."));
        }
        else
        {
            events.Report(new OperationEvent(OperationEventKind.Ok, $"Backup completo em: {pastaBackup}"));
        }

        return new BackupResultado(status, itens, pastaBackup, caminhoManifesto,
            Directory.Exists(Path.Combine(pastaBackup, "drivers-export"))
                ? Path.Combine(pastaBackup, "drivers-export")
                : null);
    }

    private static string StatusTexto(BackupItemStatus status) => status switch
    {
        BackupItemStatus.Ok => "OK",
        BackupItemStatus.Ausente => "Ausente",
        _ => "Falhou",
    };

    // ------------------------------------------------------------- restauração

    private async Task PrepararRestauracaoSistemaAsync(
        IConfirmDialog confirm, IProgress<OperationEvent> events, CancellationToken ct)
    {
        // Em Windows "mexidos" o VSS costuma estar desabilitado (causa o erro
        // 'serviço desabilitado ou sem dispositivos associados').
        foreach (string svc in new[] { "VSS", "swprv" })
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{svc}", writable: true);
            if (key?.GetValue("Start") is int start && start == 4)
            {
                try
                {
                    key.SetValue("Start", 3, RegistryValueKind.DWord);
                    events.Report(new OperationEvent(OperationEventKind.Ok,
                        $"Serviço {svc} estava DESABILITADO — reativado como Manual."));
                }
                catch (Exception ex)
                {
                    events.Report(new OperationEvent(OperationEventKind.Error,
                        $"Não foi possível reativar o serviço {svc}: {ex.Message}"));
                }
            }
        }

        // Política que desliga a Restauração por completo (corporativa ou de "otimizador").
        using (RegistryKey? pol = Registry.LocalMachine.OpenSubKey(
                   @"SOFTWARE\Policies\Microsoft\Windows NT\SystemRestore", writable: true))
        {
            if (pol?.GetValue("DisableSR") is int disable && disable == 1)
            {
                if (await confirm.ConfirmAsync("Restauração do Sistema",
                        "A Restauração do Sistema está desabilitada por política (DisableSR=1).\nRemover o bloqueio?").ConfigureAwait(false))
                {
                    try
                    {
                        pol.SetValue("DisableSR", 0, RegistryValueKind.DWord);
                        events.Report(new OperationEvent(OperationEventKind.Ok, "Política DisableSR desativada."));
                    }
                    catch (Exception ex)
                    {
                        events.Report(new OperationEvent(OperationEventKind.Error,
                            $"Falha ao remover a política DisableSR: {ex.Message}"));
                    }
                }
                else
                {
                    events.Report(new OperationEvent(OperationEventKind.Warning,
                        "Seguindo com a política ativa (o ponto de restauração pode continuar falhando)."));
                }
            }
        }

        var probe = new PowerShellProbe(runner);
        await probe.RunAsync("try { Enable-ComputerRestore -Drive \"$env:SystemDrive\\\" } catch { }",
            ct: ct, timeoutMinutes: 2).ConfigureAwait(false);

        foreach (string svc in new[] { "VSS", "swprv" })
        {
            try
            {
                using var sc = new System.ServiceProcess.ServiceController(svc);
                if (sc.Status == System.ServiceProcess.ServiceControllerStatus.Stopped)
                    await Task.Run(() => sc.Start(), ct).ConfigureAwait(false);
            }
            catch { }
        }
    }

    // ------------------------------------------------------------- ponto de restauração

    private const string RestorePointScriptTemplate = """
        $before = @(Get-ComputerRestorePoint -ErrorAction SilentlyContinue).Count
        try {
            Checkpoint-Computer -Description "Pre reparo de audio {STAMP}" -RestorePointType MODIFY_SETTINGS -ErrorAction Stop
            $after = @(Get-ComputerRestorePoint -ErrorAction SilentlyContinue).Count
            if ($after -gt $before) { Write-Output 'RESULT=OK' } else { Write-Output 'RESULT=SEM-PONTO' }
        } catch {
            Write-Output ('RESULT=FALHA: ' + $_.Exception.Message)
        }
        """;

    private async Task<BackupItem> CriarPontoRestauracaoAsync(
        RunContext run,
        IConfirmDialog confirm,
        IProgress<OperationEvent> events,
        CancellationToken ct)
    {
        string script = RestorePointScriptTemplate.Replace("{STAMP}", run.RunStamp);
        var probe = new PowerShellProbe(runner);

        events.Report(new OperationEvent(OperationEventKind.Info, "Criando ponto de restauração..."));
        ProcessResult first = await probe.RunAsync(script, ct: ct, timeoutMinutes: 15).ConfigureAwait(false);
        (BackupItem item, bool tentarDeNovo) = InterpretarPonto(first, "criado e confirmado");
        if (!tentarDeNovo) return item;

        // Limite padrão do Windows: 1 ponto a cada 24h engole a criação silenciosamente.
        events.Report(new OperationEvent(OperationEventKind.Warning,
            "Nenhum ponto novo (limite de 1 ponto/24h pode ter bloqueado)."));
        if (!await confirm.ConfirmAsync("Ponto de restauração",
                "Liberar criação imediata (frequência=0) e tentar de novo?").ConfigureAwait(false))
        {
            return new BackupItem("Ponto de restauração", BackupItemStatus.Falhou,
                "operador optou por não liberar a frequência de 24h");
        }

        const string srKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore";
        object? oldFreq = null;
        bool tinhaFreq = false;
        using (RegistryKey? freq = Registry.LocalMachine.OpenSubKey(srKey, writable: true))
        {
            if (freq is not null)
            {
                oldFreq = freq.GetValue("SystemRestorePointCreationFrequency");
                tinhaFreq = oldFreq is not null;
                freq.SetValue("SystemRestorePointCreationFrequency", 0, RegistryValueKind.DWord);
            }
        }

        try
        {
            ProcessResult second = await probe.RunAsync(script, ct: ct, timeoutMinutes: 15).ConfigureAwait(false);
            (item, _) = InterpretarPonto(second, "criado após liberar frequência");
            if (item.Status == BackupItemStatus.Falhou)
            {
                item = item with { Detalhe = "checkpoint não criou ponto nem com frequência liberada" };
            }
            return item;
        }
        finally
        {
            using RegistryKey? freq = Registry.LocalMachine.OpenSubKey(srKey, writable: true);
            if (freq is not null)
            {
                if (tinhaFreq) freq.SetValue("SystemRestorePointCreationFrequency", oldFreq!, RegistryValueKind.DWord);
                else freq.DeleteValue("SystemRestorePointCreationFrequency", throwOnMissingValue: false);
            }
        }
    }

    private static (BackupItem Item, bool TentarDeNovo) InterpretarPonto(ProcessResult result, string detalheOk)
    {
        string texto = result.OutputText;
        if (texto.Contains("RESULT=OK", StringComparison.Ordinal))
            return (new BackupItem("Ponto de restauração", BackupItemStatus.Ok, detalheOk), false);

        Match falha = Regex.Match(texto, @"RESULT=FALHA: (.*)");
        if (falha.Success)
        {
            return (new BackupItem("Ponto de restauração", BackupItemStatus.Falhou, falha.Groups[1].Value.Trim()), false);
        }

        return (new BackupItem("Ponto de restauração", BackupItemStatus.Falhou, "sem ponto novo"), true);
    }

    // ------------------------------------------------------------- drivers

    public Task<string?> FindExistingDriverExportAsync(string appDataRoot, CancellationToken ct)
    {
        string? resultado = FindDriverExportUnder(appDataRoot);
        return Task.FromResult(resultado);
    }

    internal static string? FindDriverExportUnder(string appDataRoot)
    {
        string execucoes = Path.Combine(appDataRoot, "execucoes");
        if (!Directory.Exists(execucoes)) return null;

        foreach (string runDir in Directory.EnumerateDirectories(execucoes).OrderByDescending(Path.GetFileName))
        {
            string candidatos = Path.Combine(runDir, "backup", "drivers-export");
            if (Directory.Exists(candidatos) && Directory.EnumerateFileSystemEntries(candidatos).Any())
                return candidatos;
        }
        return null;
    }

    private async Task<BackupItem> ExportarDriversAsync(
        string pastaBackup, IProgress<OperationEvent> events, CancellationToken ct)
    {
        string destino = Path.Combine(pastaBackup, "drivers-export");
        try
        {
            Directory.CreateDirectory(destino);
        }
        catch (Exception ex)
        {
            return new BackupItem("Export de drivers (DISM)", BackupItemStatus.Falhou, $"criar pasta: {ex.Message}");
        }

        events.Report(new OperationEvent(OperationEventKind.Info, "Exportando drivers de terceiros (DISM)..."));
        var request = new ProcessRequest(
            "dism.exe", $"/Online /Export-Driver /Destination:{Quote(destino)}", TimeoutMinutes: 120);
        ProcessResult result;
        try
        {
            result = await runner.RunAsync(request, null, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new BackupItem("Export de drivers (DISM)", BackupItemStatus.Falhou, ex.Message);
        }

        int pacotes = Directory.Exists(destino) ? Directory.EnumerateFileSystemEntries(destino).Count() : 0;
        if (result.Ok && pacotes > 0)
        {
            return new BackupItem("Export de drivers (DISM)", BackupItemStatus.Ok, $"{pacotes} pacote(s) em {destino}");
        }

        events.Report(new OperationEvent(OperationEventKind.Warning,
            "Sem backup de drivers: NÃO use a Escalada (remoção de drivers) até isso estar resolvido."));
        return new BackupItem("Export de drivers (DISM)", BackupItemStatus.Falhou,
            $"exit={result.ExitCode}; {UltimaLinha(result.OutputText)}");
    }

    // ------------------------------------------------------------- registro

    private async Task<List<BackupItem>> ExportarRegistroAsync(string pastaBackup, IProgress<OperationEvent> events)
    {
        events.Report(new OperationEvent(OperationEventKind.Info, "Exportando chaves de registro..."));
        var itens = new List<BackupItem>();
        foreach ((string subKey, string arquivo) in ChavesExport)
        {
            string arquivoCompleto = Path.Combine(pastaBackup, arquivo);
            try
            {
                ProcessResult result = await runner.RunAsync(new ProcessRequest(
                    "reg.exe", $"export {subKey} {Quote(arquivoCompleto)} /y", TimeoutMinutes: 2)).ConfigureAwait(false);

                bool arquivoValido = File.Exists(arquivoCompleto) && new FileInfo(arquivoCompleto).Length > 2;
                if (result.Ok && arquivoValido)
                {
                    itens.Add(new BackupItem($"Registro: {arquivo}", BackupItemStatus.Ok));
                }
                else if (result.ExitCode == 1)
                {
                    itens.Add(new BackupItem($"Registro: {arquivo}", BackupItemStatus.Ausente, $"chave não existe: {subKey}"));
                }
                else
                {
                    itens.Add(new BackupItem($"Registro: {arquivo}", BackupItemStatus.Falhou,
                        $"exit={result.ExitCode}; {UltimaLinha(result.OutputText)}"));
                }
            }
            catch (Exception ex)
            {
                itens.Add(new BackupItem($"Registro: {arquivo}", BackupItemStatus.Falhou, ex.Message));
            }
        }

        foreach (BackupItem item in itens.Where(i => i.Status == BackupItemStatus.Falhou))
        {
            events.Report(new OperationEvent(OperationEventKind.Error, $"Registro {item.Nome}: {item.Detalhe}"));
        }
        return itens;
    }

    internal static string Quote(string path) => $"\"{path}\"";

    /// <summary>Export de drivers sob demanda (usado pela Escalada antes de remover).</summary>
    public async Task<BackupItem> ExportarDriversParaEscaladaAsync(
        RunContext run, IProgress<OperationEvent> events, CancellationToken ct)
    {
        string pastaBackup = Path.Combine(run.Root, "backup");
        Directory.CreateDirectory(pastaBackup);
        BackupItem item = await ExportarDriversAsync(pastaBackup, events, ct).ConfigureAwait(false);
        return item;
    }

    internal static string UltimaLinha(string texto)
    {
        string[] linhas = texto.Split('\n');
        for (int i = linhas.Length - 1; i >= 0; i--)
        {
            string linha = linhas[i].Trim();
            if (linha.Length > 0) return linha;
        }
        return string.Empty;
    }
}
