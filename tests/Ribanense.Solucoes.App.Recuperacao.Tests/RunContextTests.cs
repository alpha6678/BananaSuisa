using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using Ribanense.Solucoes.App.Recuperacao.Recovery.Audio;
using Ribanense.Solucoes.App.Recuperacao.Recovery.Core;
using Xunit;

namespace Ribanense.Solucoes.App.Recuperacao.Tests;

public class RunContextTests : IDisposable
{
    private readonly string _raiz = Path.Combine(
        Path.GetTempPath(), "ribanense-recuperacao-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_raiz, recursive: true); } catch { }
    }

    [Fact]
    public void CreateNew_cria_pasta_da_execucao()
    {
        var run = RunContext.CreateNew(_raiz);

        Assert.True(Directory.Exists(run.Root));
        Assert.StartsWith(Path.Combine(_raiz, "execucoes"), run.Root);
    }

    [Fact]
    public void Manifesto_roundtrip_marca_completo()
    {
        var run = RunContext.CreateNew(_raiz);
        var manifesto = new BackupManifest(run.RunStamp, run.Root, "Completo",
            [new BackupManifestItem("Ponto de restauração", "OK", "criado e confirmado")]);

        File.WriteAllText(run.FilePath("backup-manifest.json"),
            JsonSerializer.Serialize(manifesto, BackupManifest.JsonOptions));

        Assert.True(run.TryReadManifest(out BackupManifest? lido));
        Assert.True(lido!.Completo);
        Assert.Equal("OK", lido.Items[0].Status);
    }

    [Fact]
    public void Manifesto_tolera_camelCase_e_PascalCase_na_leitura()
    {
        var run = RunContext.CreateNew(_raiz);
        File.WriteAllText(run.FilePath("backup-manifest.json"),
            """{"Timestamp":"20261027-140000","RunDir":"C:\\x","Status":"Completo","Items":[{"Item":"a","Status":"OK","Detail":""}]}""");

        Assert.True(run.TryReadManifest(out BackupManifest? lido));
        Assert.True(lido!.Completo);
    }

    [Fact]
    public void MostRecentWithManifest_encontra_execucao_mais_recente()
    {
        RunContext.CreateNew(_raiz); // execução de hoje, sem manifesto

        string dir1 = Path.Combine(_raiz, "execucoes", "20260101-000000");
        string dir2 = Path.Combine(_raiz, "execucoes", "20260102-000000");
        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);
        File.WriteAllText(Path.Combine(dir1, "backup-manifest.json"), """{"Status":"Incompleto"}""");
        File.WriteAllText(Path.Combine(dir2, "backup-manifest.json"), """{"Status":"Completo"}""");

        RunContext? encontrado = RunContext.MostRecentWithManifest(_raiz);

        Assert.NotNull(encontrado);
        Assert.Equal("20260102-000000", encontrado!.RunStamp);
        Assert.True(encontrado.TryReadManifest(out BackupManifest? m));
        Assert.True(m!.Completo);
    }

    [Fact]
    public void Manifesto_ausente_devolve_false()
    {
        var run = RunContext.CreateNew(_raiz);

        Assert.False(run.TryReadManifest(out _));
    }
}

public class AudioBackupServiceTests
{
    [Fact]
    public void Quote_envolve_o_caminho_com_aspas()
    {
        Assert.Equal("\"C:\\pasta\\arquivo.reg\"", AudioBackupService.Quote("C:\\pasta\\arquivo.reg"));
    }

    [Fact]
    public void UltimaLinha_devolve_ultima_linha_nao_vazia()
    {
        Assert.Equal("exit=3", AudioBackupService.UltimaLinha("linha 1\nlinha 2\nexit=3\n"));
        Assert.Equal(string.Empty, AudioBackupService.UltimaLinha("  \n  "));
    }

    [Fact]
    public void FindDriverExportUnder_ignora_execucoes_sem_export()
    {
        string raiz = Path.Combine(
            Path.GetTempPath(), "ribanense-recuperacao-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            string vazio = Path.Combine(raiz, "execucoes", "20260101-000000", "backup", "drivers-export");
            string cheio = Path.Combine(raiz, "execucoes", "20260102-000000", "backup", "drivers-export");
            Directory.CreateDirectory(vazio);
            Directory.CreateDirectory(cheio);
            File.WriteAllText(Path.Combine(cheio, "oem1.inf"), "x");

            string? encontrado = AudioBackupService.FindDriverExportUnder(raiz);

            Assert.Equal(cheio, encontrado);
        }
        finally
        {
            try { Directory.Delete(raiz, recursive: true); } catch { }
        }
    }
}

public class AudioRepairServiceTests
{
    private sealed class RunnerNulo : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessRequest request, IProgress<string>? output = null, CancellationToken ct = default) =>
            throw new NotSupportedException("não deve ser chamado neste teste");
    }

    [Fact]
    public void GetStepNames_inclui_sfc_e_dism_por_padrao()
    {
        var service = new AudioRepairService(new RunnerNulo());
        var passos = service.GetStepNames(new ReparoOpcoes());

        Assert.Contains("SFC /scannow", passos);
        Assert.Contains("DISM /RestoreHealth", passos);
        Assert.Equal(12, passos.Count);
    }

    [Fact]
    public void GetStepNames_omite_sfc_e_dism_quando_pulados()
    {
        var service = new AudioRepairService(new RunnerNulo());
        var passos = service.GetStepNames(new ReparoOpcoes(SkipSfc: true, SkipDism: true));

        Assert.DoesNotContain("SFC /scannow", passos);
        Assert.DoesNotContain("DISM /RestoreHealth", passos);
        Assert.Equal(10, passos.Count);
    }
}
