using System;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Ribanense.Solucoes.App.Recuperacao.Recovery.Core;

namespace Ribanense.Solucoes.App.Recuperacao.Recovery.Audio;

public interface IAudioRepairService
{
    /// <summary>Nomes dos passos que serão executados com as opções dadas (para a UI pré-listar).</summary>
    IReadOnlyList<string> GetStepNames(ReparoOpcoes opcoes);

    /// <summary>Executa a fase 3 (reparo seguro — não apaga o Driver Store).</summary>
    Task<ReparoResultado> RunAsync(
        RunContext run,
        ReparoOpcoes opcoes,
        IConfirmDialog confirm,
        Action<ReparoPassoStatus> onStep,
        IProgress<OperationEvent> events,
        CancellationToken ct);
}

/// <summary>
/// Reparo seguro: restaura serviços, limpa políticas de bloqueio, corrige a ACL
/// do MMDevices, reabilita dispositivos, roda SFC/DISM (opcionais) e revarre o
/// hardware. NÃO remove drivers do Driver Store — porta da Fase 3 do script.
/// </summary>
public sealed class AudioRepairService(IProcessRunner runner) : IAudioRepairService
{
    private readonly PowerShellProbe probe = new(runner);

    public IReadOnlyList<string> GetStepNames(ReparoOpcoes opcoes)
    {
        var nomes = new List<string>
        {
            "Restaurar chaves de serviço",
            "Serviços em Automático",
            "Políticas de bloqueio",
            "ACL do MMDevices",
            "Dispositivos desabilitados",
            "Serviço hdaudio",
            "Codec ACPI sem driver",
        };
        if (!opcoes.SkipSfc) nomes.Add("SFC /scannow");
        if (!opcoes.SkipDism) nomes.Add("DISM /RestoreHealth");
        nomes.Add("Revarredura de hardware");
        nomes.Add("Inicializar serviços de áudio");
        nomes.Add("Verificação final");
        return nomes;
    }

    public async Task<ReparoResultado> RunAsync(
        RunContext run,
        ReparoOpcoes opcoes,
        IConfirmDialog confirm,
        Action<ReparoPassoStatus> onStep,
        IProgress<OperationEvent> events,
        CancellationToken ct)
    {
        events.Report(new OperationEvent(OperationEventKind.Info,
            "FASE 3 — REPARO SEGURO (restaura serviços, limpa políticas, corrige MMDevices, reabilita dispositivos e revarre o hardware; NÃO apaga o Driver Store)."));

        // Travamento: exige backup completo (desta execução ou de uma anterior) antes de mexer.
        if (!await TemBackupCompletoAsync(run).ConfigureAwait(false))
        {
            events.Report(new OperationEvent(OperationEventKind.Error,
                "Sem backup completo (ponto de restauração/drivers/registro)."));
            if (!await confirm.ConfirmAsync("Sem backup",
                    "Não há backup completo disponível.\nProsseguir com o reparo MESMO SEM backup completo?").ConfigureAwait(false))
            {
                events.Report(new OperationEvent(OperationEventKind.Info,
                    "Cancelado. Execute a fase de Backup primeiro."));
                return new ReparoResultado(0, 0, [new ReparoPassoStatus("Backup", ReparoEstado.Pendente, "cancelado por falta de backup")], 0, 0);
            }
            events.Report(new OperationEvent(OperationEventKind.Warning,
                "Prosseguindo sem backup por decisão explicita do operador."));
        }

        if (!await confirm.ConfirmAsync("Reparo seguro",
                "Executar o reparo seguro agora?").ConfigureAwait(false))
        {
            events.Report(new OperationEvent(OperationEventKind.Info, "Cancelado."));
            return new ReparoResultado(0, 0, [], 0, 0);
        }

        var estados = new List<ReparoPassoStatus>();
        int ok = 0, falha = 0;

        async Task PassoAsync(string nome, Func<Task<string?>> acao)
        {
            ct.ThrowIfCancellationRequested();
            onStep(new ReparoPassoStatus(nome, ReparoEstado.Executando));
            try
            {
                string? detalhe = await acao().ConfigureAwait(false);
                ok++;
                estados.Add(new ReparoPassoStatus(nome, ReparoEstado.Ok, detalhe));
                events.Report(new OperationEvent(OperationEventKind.Ok, $"{nome}: concluído{(string.IsNullOrEmpty(detalhe) ? "" : $" — {detalhe}")}"));
            }
            catch (OperationCanceledException)
            {
                estados.Add(new ReparoPassoStatus(nome, ReparoEstado.Falhou, "cancelado"));
                throw;
            }
            catch (Exception ex)
            {
                falha++;
                estados.Add(new ReparoPassoStatus(nome, ReparoEstado.Falhou, ex.Message));
                events.Report(new OperationEvent(OperationEventKind.Error, $"Falha em '{nome}': {ex.Message}"));
            }
        }

        await PassoAsync("Restaurar chaves de serviço", () => RestaurarChavesServicoAsync(ct)).ConfigureAwait(false);
        await PassoAsync("Serviços em Automático", () => Task.FromResult<string?>(ServicosAutomatico())).ConfigureAwait(false);
        await PassoAsync("Políticas de bloqueio", () => RepararPoliticas(confirm, events)).ConfigureAwait(false);
        await PassoAsync("ACL do MMDevices", () => Task.FromResult<string?>(string.Join(" | ", MmDevicesAcl.Repair(events)))).ConfigureAwait(false);
        await PassoAsync("Dispositivos desabilitados", () => ReabilitarDispositivos(ct)).ConfigureAwait(false);
        await PassoAsync("Serviço hdaudio", () => RestaurarHdaudioAsync(ct)).ConfigureAwait(false);
        await PassoAsync("Codec ACPI sem driver", () => RepararCodecAcpi(events, ct)).ConfigureAwait(false);

        if (!opcoes.SkipSfc)
        {
            await PassoAsync("SFC /scannow", async () =>
            {
                events.Report(new OperationEvent(OperationEventKind.Info,
                    "SFC em andamento (pode demorar; log completo salvo na pasta da execução)."));
                ProcessResult r = await runner.RunAsync(
                    new ProcessRequest("sfc.exe", "/scannow", TimeoutMinutes: 120), null, ct).ConfigureAwait(false);
                File.WriteAllLines(run.FilePath("sfc-scannow.txt"), r.Output);
                return r.Ok ? "concluído" : $"código {r.ExitCode} (veja o log)";
            }).ConfigureAwait(false);
        }

        if (!opcoes.SkipDism)
        {
            await PassoAsync("DISM /RestoreHealth", async () =>
            {
                events.Report(new OperationEvent(OperationEventKind.Info,
                    "DISM /RestoreHealth em andamento (precisa de internet; pode demorar)."));
                ProcessResult r = await runner.RunAsync(
                    new ProcessRequest("dism.exe", "/Online /Cleanup-Image /RestoreHealth", TimeoutMinutes: 120), null, ct).ConfigureAwait(false);
                File.WriteAllLines(run.FilePath("dism-restorehealth.txt"), r.Output);
                return r.Ok ? "concluído" : $"código {r.ExitCode} (imagem muito danificada ou sem fonte — reparo in-place é o caminho)";
            }).ConfigureAwait(false);
        }

        await PassoAsync("Revarredura de hardware", async () =>
        {
            ProcessResult r = await runner.RunAsync(
                new ProcessRequest("pnputil.exe", "/scan-devices", TimeoutMinutes: 10), null, ct).ConfigureAwait(false);
            return r.Ok ? "revarrida" : $"pnputil exit={r.ExitCode}";
        }).ConfigureAwait(false);

        await PassoAsync("Inicializar serviços de áudio", () => Task.FromResult<string?>(IniciarServicos())).ConfigureAwait(false);

        (int endpoints, int sons) = await VerificacaoFinalAsync(ct).ConfigureAwait(false);
        estados.Add(new ReparoPassoStatus("Verificação final", ReparoEstado.Ok,
            $"{endpoints} endpoint(s); {sons} dispositivo(s) de som (Win32_SoundDevice)"));

        if (falha > 0)
        {
            events.Report(new OperationEvent(OperationEventKind.Warning,
                $"Passos com problema: {falha} de {ok + falha}. Detalhes no log."));
        }

        if (endpoints > 0)
        {
            events.Report(new OperationEvent(OperationEventKind.Ok,
                "Endpoints visíveis! Teste o som (o mixer pode precisar de um reinício da sessão)."));
        }
        else
        {
            events.Report(new OperationEvent(OperationEventKind.Warning,
                "Ainda sem endpoints. Se o codec ACPI (ex.: ESSX8336) seguir sem driver, instale-o pelo Windows Update (Atualizações opcionais) ou pela fase Instalar Codec, reinicie e rode o Diagnóstico novamente."));
        }

        return new ReparoResultado(ok, falha, estados, endpoints, sons);
    }

    // ------------------------------------------------------------- gate de backup

    private static async Task<bool> TemBackupCompletoAsync(RunContext run)
    {
        if (run.TryReadManifest(out BackupManifest? atual) && atual!.Completo) return true;

        RunContext? anterior = RunContext.MostRecentWithManifest(ObterAppDataDoRun(run));
        return anterior is not null
            && anterior.TryReadManifest(out BackupManifest? m)
            && m!.Completo;
    }

    /// <summary>Deriva a raiz de dados (…\execucoes\..) a partir do RunContext atual.</summary>
    internal static string ObterAppDataDoRun(RunContext run)
    {
        DirectoryInfo? execucoes = Directory.GetParent(run.Root);
        return execucoes?.Parent?.FullName ?? run.Root;
    }

    // ------------------------------------------------------------- passos

    private async Task<string?> RestaurarChavesServicoAsync(CancellationToken ct)
    {
        foreach (string name in AudioConstants.LastKnownGoodServices)
        {
            string current = $@"SYSTEM\CurrentControlSet\Services\{name}";
            if (Registry.LocalMachine.OpenSubKey(current) is not null) continue;

            foreach (string alt in new[] { "ControlSet001", "ControlSet002" })
            {
                string source = $@"SYSTEM\{alt}\Services\{name}";
                if (Registry.LocalMachine.OpenSubKey(source) is null) continue;

                ProcessResult r = await runner.RunAsync(new ProcessRequest(
                    "reg.exe", $"copy HKLM\\{source} HKLM\\{current} /s /f", TimeoutMinutes: 5), null, ct).ConfigureAwait(false);
                if (r.Ok) return $"{name} restaurado de {alt}.";
                throw new InvalidOperationException($"reg copy falhou para {name}. Será preciso DISM/reparo in-place.");
            }
        }
        return "chaves de serviço presentes (nada a restaurar)";
    }

    private static string ServicosAutomatico()
    {
        foreach (string name in AudioConstants.LastKnownGoodServices)
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{name}", writable: true);
            if (key is null) continue;
            if (key.GetValue("Start") is int start && start != 2)
            {
                key.SetValue("Start", 2, RegistryValueKind.DWord);
            }
        }
        return "Audiosrv e AudioEndpointBuilder em Automático";
    }

    private async Task<string?> RepararPoliticas(IConfirmDialog confirm, IProgress<OperationEvent> events)
    {
        using RegistryKey? root = Registry.LocalMachine.OpenSubKey(AudioConstants.DeviceInstallRestrictionsPath, writable: true);
        if (root is null) return "Nenhuma política de DeviceInstall presente. Nada a fazer.";

        var aRemover = new List<(string Flag, string ValueName)>();
        foreach (string flag in new[] { "DenyDeviceIDs", "DenyDeviceClasses", "DenyInstanceIDs" })
        {
            using RegistryKey? sub = root.OpenSubKey(flag);
            if (sub is null) continue;
            foreach (string valueName in sub.GetValueNames())
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(valueName, @"^\d+$")) continue;
                string? value = sub.GetValue(valueName) as string;
                if (value is not null
                    && (string.Equals(value, AudioConstants.MediaClassGuid, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(value, AudioConstants.AudioEndpointClassGuid, StringComparison.OrdinalIgnoreCase)))
                {
                    aRemover.Add((flag, valueName));
                }
            }
        }

        if (aRemover.Count == 0) return "Nenhum bloqueio de áudio encontrado.";

        string lista = string.Join("\n", aRemover.Select(a => $"• {a.Flag}\\{a.ValueName}"));
        if (!await confirm.ConfirmAsync("Bloqueios de áudio",
                $"Remover {aRemover.Count} bloqueio(s) de instalação de áudio?\n\n{lista}").ConfigureAwait(false))
        {
            events.Report(new OperationEvent(OperationEventKind.Info, "Remoção de bloqueios cancelada pelo operador."));
            return "cancelado pelo operador";
        }

        foreach ((string flag, string valueName) in aRemover)
        {
            using RegistryKey? sub = root.OpenSubKey(flag, writable: true);
            sub?.DeleteValue(valueName, throwOnMissingValue: false);
            events.Report(new OperationEvent(OperationEventKind.Ok, $"Removido: {flag}\\{valueName}."));

            // Se a lista ficou vazia, desliga o interruptor da política.
            bool restam = sub?.GetValueNames().Any(n => System.Text.RegularExpressions.Regex.IsMatch(n, @"^\d+$")) ?? false;
            if (sub is not null && !restam)
            {
                root.SetValue(flag, 0, RegistryValueKind.DWord);
                events.Report(new OperationEvent(OperationEventKind.Ok, $"{flag} voltou para 0 (lista vazia)."));
            }
        }

        return $"{aRemover.Count} bloqueio(s) removido(s)";
    }

    private async Task<string?> ReabilitarDispositivos(CancellationToken ct)
    {
        const string script = """
            @(Get-PnpDevice -Class MEDIA -ErrorAction SilentlyContinue | Where-Object { $_.Problem -eq 22 } | Select-Object FriendlyName,InstanceId) | ConvertTo-Json -Depth 2 -Compress
            """;
        var alvos = await probe.QueryJsonListAsync<PnpRow>(script, ct: ct, timeoutMinutes: 5).ConfigureAwait(false);
        if (alvos.Count == 0) return "Nenhum dispositivo de áudio desabilitado.";

        var detalhes = new List<string>();
        foreach (PnpRow d in alvos)
        {
            if (string.IsNullOrEmpty(d.InstanceId)) continue;
            await runner.RunAsync(new ProcessRequest(
                "pnputil.exe", $"/enable-device \"{d.InstanceId}\"", TimeoutMinutes: 5), null, ct).ConfigureAwait(false);
            detalhes.Add(d.FriendlyName ?? d.InstanceId);
        }
        return $"Habilitado(s): {string.Join(", ", detalhes)}";
    }

    private async Task<string?> RestaurarHdaudioAsync(CancellationToken ct)
    {
        string current = @"SYSTEM\CurrentControlSet\Services\hdaudio";
        if (Registry.LocalMachine.OpenSubKey(current) is not null)
            return "Serviço hdaudio presente.";

        // Última instância do pacote inbox: <Windows>\INF\hdaudio.inf.
        string inf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "INF", "hdaudio.inf");
        if (File.Exists(inf))
        {
            await runner.RunAsync(new ProcessRequest(
                "pnputil.exe", $"/add-driver \"{inf}\" /install", TimeoutMinutes: 10), null, ct).ConfigureAwait(false);
        }

        if (Registry.LocalMachine.OpenSubKey(current) is not null)
            return "Pacote HDAudio reinstalado via pnputil (chave recriada).";

        // Último recurso: criar a chave com os valores padrão do driver inbox.
        using RegistryKey? key = Registry.LocalMachine.CreateSubKey(current);
        key.SetValue("Type", 1, RegistryValueKind.DWord);
        key.SetValue("Start", 3, RegistryValueKind.DWord);
        key.SetValue("ErrorControl", 1, RegistryValueKind.DWord);
        key.SetValue("ImagePath", "system32\\DRIVERS\\hdaudio.sys", RegistryValueKind.ExpandString);
        return "Chave do serviço hdaudio recriada com valores padrão.";
    }

    private async Task<string?> RepararCodecAcpi(IProgress<OperationEvent> events, CancellationToken ct)
    {
        const string script = """
            @(Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object { $_.Problem -eq 28 -and $_.InstanceId -match '^ACPI\\' } | Select-Object FriendlyName,InstanceId) | ConvertTo-Json -Depth 2 -Compress
            """;
        var ausentes = await probe.QueryJsonListAsync<PnpRow>(script, ct: ct, timeoutMinutes: 5).ConfigureAwait(false);
        if (ausentes.Count == 0) return "Nenhum codec ACPI sem driver.";

        bool essx = false;
        foreach (PnpRow d in ausentes)
        {
            events.Report(new OperationEvent(OperationEventKind.Error,
                $"Sem driver: {d.FriendlyName} ({d.InstanceId})"));
            if (d.InstanceId?.Contains("ESSX8336", StringComparison.OrdinalIgnoreCase) == true)
            {
                essx = true;
                events.Report(new OperationEvent(OperationEventKind.Warning,
                    "Codec ESS ES8336 identificado. Instalar o driver dele é O passo central deste caso:"));
                events.Report(new OperationEvent(OperationEventKind.Warning,
                    "a) Com internet: Configurações > Windows Update > Opções avançadas > Atualizações opcionais > Correções de driver (fornecedor ESS Technology)."));
                events.Report(new OperationEvent(OperationEventKind.Warning,
                    "b) Ou use a fase Instalar Codec deste app com o pacote do fabricante do notebook."));
            }
        }

        events.Report(new OperationEvent(OperationEventKind.Info,
            "Disparando varredura de drivers no Windows Update (UsoClient StartScan)..."));
        try
        {
            await runner.RunAsync(new ProcessRequest("UsoClient.exe", "StartScan", TimeoutMinutes: 2), null, ct).ConfigureAwait(false);
        }
        catch { /* UsoClient pode não emitir saída; falha silenciosa igual ao script */ }

        return essx
            ? "codec ESSX8336 sem driver — varredura WU disparada; confira Atualizações opcionais"
            : $"{ausentes.Count} codec(s) ACPI sem driver — varredura WU disparada";
    }

    private string IniciarServicos()
    {
        var estados = new List<string>();
        foreach (string name in AudioConstants.LastKnownGoodServices)
        {
            try
            {
                using var sc = new System.ServiceProcess.ServiceController(name);
                if (sc.Status != System.ServiceProcess.ServiceControllerStatus.Running)
                    sc.Start();
                sc.Refresh();
                estados.Add($"{name}: {AudioDiagnosticsService.ServiceStatusName(sc.Status)}");
            }
            catch (Exception ex)
            {
                estados.Add($"{name}: falhou ({ex.Message})");
            }
        }
        return string.Join(" | ", estados);
    }

    private async Task<(int Endpoints, int Sons)> VerificacaoFinalAsync(CancellationToken ct)
    {
        int endpoints = 0;
        try
        {
            List<int> counts = await probe.QueryJsonListAsync<int>(
                "@(Get-PnpDevice -Class AudioEndpoint -ErrorAction SilentlyContinue).Count",
                ct: ct, timeoutMinutes: 5).ConfigureAwait(false);
            endpoints = counts.Count > 0 ? counts[0] : 0;
        }
        catch { }

        int sons = 0;
        try
        {
            sons = await Task.Run(() =>
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_SoundDevice");
                return searcher.Get().Count;
            }, ct).ConfigureAwait(false);
        }
        catch { }

        return (endpoints, sons);
    }

    private sealed record PnpRow(string? FriendlyName, string? InstanceId, uint? Problem);
}
