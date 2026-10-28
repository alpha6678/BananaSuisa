using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Ribanense.Solucoes.App.Recuperacao.Recovery.Core;

namespace Ribanense.Solucoes.App.Recuperacao.Recovery.Audio;

public interface IAudioDiagnosticsService
{
    /// <summary>Executa a fase 1 completa (somente leitura) e devolve o relatório.</summary>
    Task<DiagnosticoRelatorio> RunAsync(IProgress<OperationEvent> events, CancellationToken ct);
}

/// <summary>
/// Diagnóstico somente leitura das causas mais comuns de "nenhum dispositivo de
/// saída de áudio" — porta nativa da Fase 1 do script de referência, com cada
/// seção isolada por try/catch (falha em uma não mata as demais).
/// </summary>
public sealed class AudioDiagnosticsService(IProcessRunner runner) : IAudioDiagnosticsService
{
    private readonly PowerShellProbe probe = new(runner);

    public async Task<DiagnosticoRelatorio> RunAsync(IProgress<OperationEvent> events, CancellationToken ct)
    {
        bool elevado = AdminCheck.IsCurrentUserAdmin;
        events.Report(new OperationEvent(OperationEventKind.Info, "FASE 1 — DIAGNÓSTICO (somente leitura)."));

        var secoes = new List<DiagnosticoSecao>
        {
            await SafeAsync("Serviços de áudio", () => Task.FromResult(CheckServices()), ct).ConfigureAwait(false),
            await SafeAsync("Políticas de bloqueio de instalação de dispositivos", () => Task.FromResult(CheckDeviceInstallPolicies()), ct).ConfigureAwait(false),
            await SafeAsync("Registro de endpoints (MMDevices)", () => Task.FromResult(CheckMmDevices()), ct).ConfigureAwait(false),
            await SafeAsync("Arquivos base de áudio", () => Task.FromResult(CheckSystemFiles()), ct).ConfigureAwait(false),
            await SafeAsync("Dispositivos PnP de áudio", () => CheckPnPAsync(ct), ct).ConfigureAwait(false),
            await SafeAsync("Driver Store", () => CheckDriverStoreAsync(ct), ct).ConfigureAwait(false),
            await SafeAsync("Diversos (internet/log/build)", () => CheckMiscAsync(ct), ct).ConfigureAwait(false),
        };

        var relatorio = new DiagnosticoRelatorio(DateTime.Now, elevado, secoes);
        events.Report(new OperationEvent(OperationEventKind.Info,
            $"Diagnóstico concluído: {relatorio.TotalErros} erro(s), {relatorio.TotalAvisos} aviso(s)."));
        return relatorio;
    }

    private static async Task<DiagnosticoSecao> SafeAsync(
        string titulo, Func<Task<List<CheckItem>>> action, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            return new DiagnosticoSecao(titulo, await action().ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var item = new CheckItem(
                $"{titulo}#erro", $"{titulo} — falha na coleta", CheckSeverity.Warning, ex.Message,
                "A seção falhou, mas as demais continuam válidas.");
            return new DiagnosticoSecao(titulo, [item], ex.Message);
        }
    }

    // ------------------------------------------------------------------ serviços

    internal static string StartTypeName(int value) => value switch
    {
        0 => "Boot",
        1 => "System",
        2 => "Automático",
        3 => "Manual",
        4 => "DESABILITADO",
        _ => $"Desconhecido({value})",
    };

    internal static string ServiceStatusName(ServiceControllerStatus status) => status switch
    {
        ServiceControllerStatus.Running => "Em execução",
        ServiceControllerStatus.Stopped => "Parado",
        ServiceControllerStatus.StartPending => "Iniciando",
        ServiceControllerStatus.StopPending => "Parando",
        ServiceControllerStatus.Paused => "Pausado",
        ServiceControllerStatus.PausePending => "Pausando",
        ServiceControllerStatus.ContinuePending => "Retomando",
        _ => status.ToString(),
    };

    private static List<CheckItem> CheckServices()
    {
        var items = new List<CheckItem>();
        foreach (string name in AudioConstants.CoreServices)
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{name}");
            if (key is null)
            {
                items.Add(new CheckItem($"svc-{name}", name, CheckSeverity.Error,
                    "Chave do serviço NÃO EXISTE (provavelmente apagada por intervenção manual).",
                    "O Reparo pode restaurar de ControlSet001/002 (LastKnownGood)."));
                continue;
            }

            int start = key.GetValue("Start") as int? ?? 3;
            string statusTexto;
            try
            {
                using var sc = new ServiceController(name);
                statusTexto = ServiceStatusName(sc.Status);
            }
            catch
            {
                statusTexto = "não instalado/ilegível";
            }

            items.Add(new CheckItem($"svc-{name}", name, CheckSeverity.Info,
                $"Start={StartTypeName(start)} | Estado={statusTexto}"));

            if ((name is "Audiosrv" or "AudioEndpointBuilder") && start == 4)
            {
                items.Add(new CheckItem($"svc-{name}-desabilitado", name, CheckSeverity.Error,
                    "Serviço DESABILITADO no registro. Isso sozinho já derruba o áudio.",
                    "O Reparo volta o serviço para Automático."));
            }
        }

        foreach (string name in AudioConstants.LastKnownGoodServices)
        {
            using RegistryKey? good = Registry.LocalMachine.OpenSubKey($@"SYSTEM\ControlSet001\Services\{name}");
            using RegistryKey? current = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{name}");
            if (good is not null && current is null)
            {
                items.Add(new CheckItem($"lkg-{name}", name, CheckSeverity.Warning,
                    "Existe em ControlSet001 (LastKnownGood) mas sumiu do CurrentControlSet.",
                    "Recuperável na fase de Reparo (reg copy)."));
            }
        }

        return items;
    }

    // ------------------------------------------------------------------ políticas

    private static readonly string[] DenyFlags = ["DenyDeviceIDs", "DenyDeviceClasses", "DenyInstanceIDs"];

    private static List<CheckItem> CheckDeviceInstallPolicies()
    {
        var items = new List<CheckItem>();
        using RegistryKey? root = Registry.LocalMachine.OpenSubKey(AudioConstants.DeviceInstallRestrictionsPath);
        if (root is null)
        {
            items.Add(new CheckItem("pol-nenhuma", "Sem bloqueios", CheckSeverity.Ok,
                "Nenhuma política de DeviceInstall presente."));
            return items;
        }

        foreach (string flag in DenyFlags)
        {
            if (root.GetValue(flag) is not int || (int)root.GetValue(flag)! != 1) continue;

            items.Add(new CheckItem($"pol-{flag}", flag, CheckSeverity.Warning,
                "Política de bloqueio ativa (flag = 1)."));

            using RegistryKey? sub = root.OpenSubKey(flag);
            if (sub is null) continue;

            foreach (string valueName in sub.GetValueNames())
            {
                if (!Regex.IsMatch(valueName, @"^\d+$")) continue;
                string? value = sub.GetValue(valueName) as string;
                if (string.IsNullOrEmpty(value)) continue;

                bool bloqueiaAudio = string.Equals(value, AudioConstants.MediaClassGuid, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(value, AudioConstants.AudioEndpointClassGuid, StringComparison.OrdinalIgnoreCase);
                items.Add(bloqueiaAudio
                    ? new CheckItem($"pol-{flag}-{valueName}", $"{flag}\\{valueName}", CheckSeverity.Error,
                        $"Bloqueio de ÁUDIO detectado: {value}",
                        "O Reparo remove esta entrada (com confirmação).")
                    : new CheckItem($"pol-{flag}-{valueName}", $"{flag}\\{valueName}", CheckSeverity.Info,
                        $"Bloqueia: {value} (não é áudio)."));
            }
        }

        return items;
    }

    // ------------------------------------------------------------------ MMDevices

    private static List<CheckItem> CheckMmDevices()
    {
        var items = new List<CheckItem>();
        using RegistryKey? baseKey = Registry.LocalMachine.OpenSubKey(AudioConstants.MmDevicesAudioPath);
        if (baseKey is null)
        {
            items.Add(new CheckItem("mm-ausente", "MMDevices\\Audio ausente", CheckSeverity.Error,
                $"Chave {AudioConstants.MmDevicesAudioPath} NÃO EXISTE. O Windows não consegue criar endpoints (causa clássica de 'nenhum dispositivo de saída').",
                "Verifique primeiro os serviços; o Reparo corrige a ACL da chave quando ela existe."));
            return items;
        }

        foreach (string side in new[] { "Render", "Capture" })
        {
            using RegistryKey? sideKey = baseKey.OpenSubKey(side);
            int count = sideKey?.GetSubKeyNames().Length ?? 0;
            items.Add(new CheckItem($"mm-{side}", side, CheckSeverity.Info, $"{count} endpoint(s) no registro."));
        }

        (CheckSeverity severidade, string texto) = MmDevicesAcl.Evaluate(baseKey);
        items.Add(new CheckItem("mm-acl", "ACL do MMDevices", severidade, texto));
        return items;
    }

    // ------------------------------------------------------------------ arquivos

    private static List<CheckItem> CheckSystemFiles()
    {
        var items = new List<CheckItem>();
        int missing = 0;
        foreach (string template in AudioConstants.SystemFiles)
        {
            string path = Environment.ExpandEnvironmentVariables(template);
            if (File.Exists(path))
            {
                items.Add(new CheckItem($"file-{template}", Path.GetFileName(path), CheckSeverity.Ok, "Presente."));
            }
            else
            {
                missing++;
                items.Add(new CheckItem($"file-{template}", Path.GetFileName(path), CheckSeverity.Error,
                    $"AUSENTE: {path}"));
            }
        }

        if (missing > 0)
        {
            items.Add(new CheckItem("files-conclusao", "Conclusão", CheckSeverity.Error,
                $"{missing} arquivo(s) de áudio nativo(s) ausentes.",
                "Indica Windows removido/enxugado (NTLite/tiny11/Atlas) ou arquivos corrompidos. Caminho provável: DISM /RestoreHealth + SFC e, se falhar, reparo in-place com ISO oficial."));
        }
        else
        {
            items.Add(new CheckItem("files-conclusao", "Binários nativos", CheckSeverity.Ok,
                "Binários de áudio nativos presentes."));
        }

        return items;
    }

    // ------------------------------------------------------------------ PnP

    private sealed record PnpDeviceRow(string? Status, uint? Problem, string? ProblemDescription, string? FriendlyName, string? InstanceId);

    private sealed record PnpBundle(
        List<PnpDeviceRow>? Controladores,
        List<PnpDeviceRow>? Media,
        int? Endpoints,
        List<string>? PnputilProblemas)
    {
        public List<PnpDeviceRow> ControladoresSafe => Controladores ?? [];
        public List<PnpDeviceRow> MediaSafe => Media ?? [];
        public List<string> ProblemasSafe => PnputilProblemas ?? [];
    }

    internal static string BuildPnpScript() => """
        $ctl = @(Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object { $_.InstanceId -match '^PCI\\' -and $_.InstanceId -match '&CC_040[13]' } | Select-Object Status,Problem,ProblemDescription,FriendlyName,InstanceId)
        $media = @(Get-PnpDevice -Class MEDIA -ErrorAction SilentlyContinue | Select-Object Status,Problem,ProblemDescription,FriendlyName,InstanceId)
        $ep = @(Get-PnpDevice -Class AudioEndpoint -ErrorAction SilentlyContinue).Count
        $prob = @(pnputil.exe /enum-devices /problem 2>$null)
        @{ controladores = $ctl; media = $media; endpoints = $ep; pnputilProblemas = $prob } | ConvertTo-Json -Depth 3 -Compress
        """;

    private async Task<List<CheckItem>> CheckPnPAsync(CancellationToken ct)
    {
        var items = new List<CheckItem>();
        PnpBundle? bundle = await probe.QueryJsonObjectAsync<PnpBundle>(
            BuildPnpScript(), ct: ct, timeoutMinutes: 5).ConfigureAwait(false);
        bundle ??= new PnpBundle(null, null, null, null);

        if (bundle.ControladoresSafe.Count == 0)
        {
            items.Add(new CheckItem("pnp-ctl", "Controladores PCI de áudio", CheckSeverity.Warning,
                "Nenhum controlador PCI de áudio (CC_0401/CC_0403) enumerado.",
                "Verifique: BIOS desabilitou áudio? Driver de chipset/Intel SST ausente?"));
        }
        else
        {
            foreach (var c in bundle.ControladoresSafe)
            {
                items.Add(new CheckItem($"pnp-ctl-{c.InstanceId}", "Controlador", CheckSeverity.Info,
                    $"{c.FriendlyName} | Status={c.Status} | Problema={c.Problem} | {c.InstanceId}"));
            }
        }

        if (bundle.MediaSafe.Count == 0)
        {
            items.Add(new CheckItem("pnp-media", "Classe MEDIA", CheckSeverity.Error,
                "Nenhum dispositivo da classe MEDIA. O subsistema de áudio está vazio."));
        }
        else
        {
            foreach (var d in bundle.MediaSafe.Where(d => (d.Problem ?? 0) != 0))
            {
                string? dica = (d.Problem ?? 0) switch
                {
                    22 => "Dispositivo desabilitado: o Reparo reabilita via pnputil.",
                    28 => "Codec sem driver: instale pelo Windows Update (Atualizações opcionais) ou use a fase Instalar Codec.",
                    _ => null,
                };
                items.Add(new CheckItem($"pnp-media-{d.InstanceId}", $"PROBLEMA {d.Problem}: {d.FriendlyName}",
                    CheckSeverity.Warning,
                    $"{d.InstanceId} -> {d.ProblemDescription ?? "sem descrição"}", dica));
            }
            int ok = bundle.MediaSafe.Count(d => (d.Problem ?? 0) == 0);
            items.Add(new CheckItem("pnp-media-ok", "Classe MEDIA", CheckSeverity.Ok,
                $"{ok} dispositivo(s) OK de {bundle.MediaSafe.Count}."));
        }

        items.Add(new CheckItem("pnp-endpoints", "Endpoints de áudio", CheckSeverity.Info,
            $"{bundle.Endpoints ?? 0} endpoint(s)."));

        if (bundle.ProblemasSafe.Count > 0)
        {
            string detalhe = string.Join(" | ", bundle.ProblemasSafe
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Trim())
                .Take(12));
            if (detalhe.Length > 300) detalhe = detalhe[..300] + "…";
            items.Add(new CheckItem("pnp-problemas", "pnputil /enum-devices /problem", CheckSeverity.Info,
                detalhe,
                "Saída completa no relatório salvo na pasta da execução."));
        }

        return items;
    }

    // ------------------------------------------------------------------ driver store

    private sealed record DriverStoreRow(string? Driver, string? ProviderName, string? Version, string? Date);

    internal static string BuildDriverStoreScript() => """
        Get-WindowsDriver -Online -All -ErrorAction SilentlyContinue |
            Where-Object { $_.ClassName -eq 'MEDIA' -or $_.ProviderName -match 'Realtek|Intel|AMD|NVIDIA' } |
            Select-Object Driver,ProviderName,Version,Date | ConvertTo-Json -Depth 2 -Compress
        """;

    private async Task<List<CheckItem>> CheckDriverStoreAsync(CancellationToken ct)
    {
        var items = new List<CheckItem>();
        if (!AdminCheck.IsCurrentUserAdmin)
        {
            items.Add(new CheckItem("store-elevacao", "Driver Store", CheckSeverity.Warning,
                "Inventário do Driver Store requer elevação — rode o Diagnóstico novamente como administrador para vê-lo."));
            return items;
        }

        List<DriverStoreRow> rows = await probe.QueryJsonListAsync<DriverStoreRow>(
            BuildDriverStoreScript(), ct: ct, timeoutMinutes: 15).ConfigureAwait(false);

        if (rows.Count == 0)
        {
            items.Add(new CheckItem("store-vazio", "Driver Store", CheckSeverity.Warning,
                "Nenhum driver de terceiros de áudio no store (máquina pode depender 100% do Windows Update)."));
            return items;
        }

        foreach (var r in rows)
        {
            items.Add(new CheckItem($"store-{r.Driver}", r.Driver ?? "?", CheckSeverity.Info,
                $"{r.ProviderName} | versão {r.Version} | data {PowerShellProbe.NormalizePsDate(r.Date) ?? "?"}"));
        }

        return items;
    }

    // ------------------------------------------------------------------ diversos

    private async Task<List<CheckItem>> CheckMiscAsync(CancellationToken ct)
    {
        var items = new List<CheckItem>();

        // setupapi.dev.log mora em <Windows>\INF.
        string log = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), "INF", "setupapi.dev.log");
        if (File.Exists(log))
        {
            int deletions = 0;
            foreach (string line in File.ReadLines(log))
            {
                if (line.Contains("delete-driver", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("remove-device", StringComparison.OrdinalIgnoreCase))
                {
                    deletions++;
                }
            }
            items.Add(new CheckItem("misc-setupapi", "setupapi.dev.log", CheckSeverity.Info,
                $"{deletions} registro(s) de remoção de driver/dispositivo (cópia salva na pasta do relatório).",
                deletions > 0 ? "Rastros de intervenção manual recentes." : null));
        }

        bool online = await TestInternetAsync(ct).ConfigureAwait(false);
        items.Add(new CheckItem("misc-internet", "Internet (Windows Update)",
            online ? CheckSeverity.Ok : CheckSeverity.Warning,
            online ? "download.windowsupdate.com acessível." :
            "Sem acesso a download.windowsupdate.com: recomposição de drivers via WU não vai funcionar."));

        using RegistryKey? cv = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        if (cv is not null)
        {
            string produto = cv.GetValue("ProductName") as string ?? "?";
            string display = cv.GetValue("DisplayVersion") as string ?? "?";
            string build = cv.GetValue("CurrentBuild") as string ?? "?";
            int ubr = cv.GetValue("UBR") as int? ?? 0;
            items.Add(new CheckItem("misc-os", "Windows", CheckSeverity.Info,
                $"{produto} {display} build {build}.{ubr}"));
        }

        return items;
    }

    private static async Task<bool> TestInternetAsync(CancellationToken ct)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using HttpResponseMessage _ = await http.GetAsync(
                "https://download.windowsupdate.com/",
                HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            return true; // qualquer resposta HTTP prova conectividade
        }
        catch
        {
            return false;
        }
    }
}
