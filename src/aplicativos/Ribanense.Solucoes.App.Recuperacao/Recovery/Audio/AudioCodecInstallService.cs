using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ribanense.Solucoes.App.Recuperacao.Recovery.Core;

namespace Ribanense.Solucoes.App.Recuperacao.Recovery.Audio;

public interface IAudioCodecInstallService
{
    /// <summary>Procura arquivos .inf na pasta (recursivo).</summary>
    Task<IReadOnlyList<CodecInfInfo>> ScanInfsAsync(string pasta, CancellationToken ct);

    /// <summary>Instala o driver por pnputil, revarre e verifica codec/endpoints.</summary>
    Task<CodecInstallResultado> InstallAsync(string infPath, IProgress<OperationEvent> events, CancellationToken ct);
}

/// <summary>
/// Instala o driver do codec (ex.: ESS ES8336) a partir de uma pasta local com
/// .inf, roda o pnputil e verifica o estado do codec e dos endpoints — porta da
/// Fase 5 do script de referência.
/// </summary>
public sealed class AudioCodecInstallService(IProcessRunner runner) : IAudioCodecInstallService
{
    private readonly PowerShellProbe probe = new(runner);

    public Task<IReadOnlyList<CodecInfInfo>> ScanInfsAsync(string pasta, CancellationToken ct)
    {
        return Task.Run<IReadOnlyList<CodecInfInfo>>(() =>
        {
            string limpa = pasta.Trim('"').Trim();
            if (!Directory.Exists(limpa)) return [];

            return Directory
                .EnumerateFiles(limpa, "*.inf", SearchOption.AllDirectories)
                .Select(f => new CodecInfInfo(f, Path.GetFileName(f)))
                .OrderBy(f => f.Nome, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }, ct);
    }

    public async Task<CodecInstallResultado> InstallAsync(string infPath, IProgress<OperationEvent> events, CancellationToken ct)
    {
        var mensagens = new List<string>();
        events.Report(new OperationEvent(OperationEventKind.Info, $"FASE 5 — INSTALAR DRIVER DO CODEC: {infPath}"));

        ProcessResult add = await runner.RunAsync(
            new ProcessRequest("pnputil.exe", $"/add-driver \"{infPath}\" /install", TimeoutMinutes: 30),
            null, ct).ConfigureAwait(false);
        bool instalou = add.Ok;
        if (instalou)
        {
            mensagens.Add("Driver adicionado/instalado pelo pnputil.");
            events.Report(new OperationEvent(OperationEventKind.Ok, "Driver adicionado/instalado pelo pnputil."));
        }
        else
        {
            mensagens.Add($"pnputil terminou com exit={add.ExitCode}.");
            events.Report(new OperationEvent(OperationEventKind.Warning,
                $"pnputil terminou com exit={add.ExitCode} (veja a saída no log)."));
        }

        events.Report(new OperationEvent(OperationEventKind.Info, "Revarrendo dispositivos..."));
        try
        {
            await runner.RunAsync(new ProcessRequest("pnputil.exe", "/scan-devices", TimeoutMinutes: 10), null, ct).ConfigureAwait(false);
        }
        catch { }

        // Estado do codec ESSX8336 + endpoints na sequência.
        const string script = """
            @(Get-PnpDevice -ErrorAction SilentlyContinue | Where-Object { $_.InstanceId -match 'ESSX8336' } | Select-Object Problem,ProblemDescription,FriendlyName,InstanceId) | ConvertTo-Json -Depth 2 -Compress
            """;
        var codecs = await probe.QueryJsonListAsync<CodecRow>(script, ct: ct, timeoutMinutes: 5).ConfigureAwait(false);
        foreach (CodecRow d in codecs)
        {
            if ((d.Problem ?? 0) == 0)
            {
                mensagens.Add($"Codec instalado e operante: {d.FriendlyName}");
                events.Report(new OperationEvent(OperationEventKind.Ok,
                    $"Codec instalado e operante: {d.FriendlyName}"));
            }
            else
            {
                string msg = $"Codec ainda com problema {d.Problem} ({d.ProblemDescription}). Reinicie e rode o Diagnóstico.";
                mensagens.Add(msg);
                events.Report(new OperationEvent(OperationEventKind.Warning, msg));
            }
        }
        if (codecs.Count == 0)
        {
            mensagens.Add("ESSX8336 não apareceu após a instalação (reboot pode ser necessário).");
            events.Report(new OperationEvent(OperationEventKind.Warning,
                "ESSX8336 não apareceu após a instalação (reboot pode ser necessário)."));
        }

        foreach (string svc in new[] { "AudioEndpointBuilder", "Audiosrv" })
        {
            try
            {
                using var sc = new System.ServiceProcess.ServiceController(svc);
                if (sc.Status != System.ServiceProcess.ServiceControllerStatus.Running)
                    await Task.Run(() => sc.Start(), ct).ConfigureAwait(false);
            }
            catch { }
        }

        int endpoints = 0;
        try
        {
            List<int> counts = await probe.QueryJsonListAsync<int>(
                "@(Get-PnpDevice -Class AudioEndpoint -ErrorAction SilentlyContinue).Count",
                ct: ct, timeoutMinutes: 5).ConfigureAwait(false);
            endpoints = counts.Count > 0 ? counts[0] : 0;
        }
        catch { }

        events.Report(new OperationEvent(OperationEventKind.Info, $"Endpoints de áudio agora: {endpoints}"));
        if (endpoints > 0)
        {
            mensagens.Add("Endpoints criados! Teste o som.");
            events.Report(new OperationEvent(OperationEventKind.Ok, "Endpoints criados! Teste o som."));
        }
        else
        {
            mensagens.Add("Sem endpoints ainda: confira a ACL do MMDevices com o Reparo e reinicie.");
            events.Report(new OperationEvent(OperationEventKind.Warning,
                "Sem endpoints ainda: confira a ACL do MMDevices com o Reparo (fase 3) e reinicie."));
        }

        return new CodecInstallResultado(instalou, codecs.Count > 0 ? codecs[0].ProblemDescription : null, endpoints, mensagens);
    }

    private sealed record CodecRow(string? FriendlyName, string? InstanceId, uint? Problem, string? ProblemDescription);
}
