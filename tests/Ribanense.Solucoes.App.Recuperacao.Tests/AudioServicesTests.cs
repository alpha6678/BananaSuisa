using System;
using System.Collections.Generic;
using System.Linq;
using Ribanense.Solucoes.App.Recuperacao.Recovery.Audio;
using Xunit;

namespace Ribanense.Solucoes.App.Recuperacao.Tests;

public class AudioEscalationServiceTests
{
    [Fact]
    public void ExtrairDiretorio_pega_o_caminho_apos_em()
    {
        Assert.Equal("C:\\backup\\drivers-export",
            AudioEscalationService.ExtrairDiretorio("12 pacote(s) em C:\\backup\\drivers-export"));
    }

    [Fact]
    public void GuiaInPlace_menciona_a_iso_oficial()
    {
        Assert.Contains("https://www.microsoft.com/pt-br/software-download/windows11",
            AudioEscalationService.GuiaInPlace);
        Assert.Contains("Manter arquivos e aplicativos pessoais", AudioEscalationService.GuiaInPlace);
    }

    [Fact]
    public void ScriptDeDrivers_filtra_classe_media()
    {
        string script = AudioEscalationService.BuildMediaDriversScript();

        Assert.Contains("ClassName -eq 'MEDIA'", script);
        Assert.Contains("Get-WindowsDriver -Online -All", script);
    }
}

public class AudioDiagnosticsServiceTests
{
    [Theory]
    [InlineData(0, "Boot")]
    [InlineData(1, "System")]
    [InlineData(2, "Automático")]
    [InlineData(3, "Manual")]
    [InlineData(4, "DESABILITADO")]
    [InlineData(9, "Desconhecido(9)")]
    public void StartTypeName_rotula_os_tipos(int valor, string esperado)
    {
        Assert.Equal(esperado, AudioDiagnosticsService.StartTypeName(valor));
    }

    [Fact]
    public void ScriptDePnp_cobre_controladores_media_e_endpoints()
    {
        string script = AudioDiagnosticsService.BuildPnpScript();

        Assert.Contains("CC_040[13]", script);
        Assert.Contains("Get-PnpDevice -Class MEDIA", script);
        Assert.Contains("Get-PnpDevice -Class AudioEndpoint", script);
        Assert.Contains("pnputil.exe /enum-devices /problem", script);
    }

    [Fact]
    public void ScriptDeDriverStore_filtra_media_e_fornecedores()
    {
        string script = AudioDiagnosticsService.BuildDriverStoreScript();

        Assert.Contains("Realtek|Intel|AMD|NVIDIA", script);
    }
}

public class AudioConstantsTests
{
    [Fact]
    public void Guids_e_sids_correspondem_ao_windows()
    {
        Assert.Equal("{4d36e96c-e325-11ce-bfc1-08002be10318}", AudioConstants.MediaClassGuid);
        Assert.Equal("{cdf10ab0-12ef-11d0-9d0c-00c04fc9a425}", AudioConstants.AudioEndpointClassGuid);
        Assert.Equal("S-1-5-18", AudioConstants.SidSystem);
        Assert.Equal("S-1-5-32-544", AudioConstants.SidAdministrators);
        Assert.Equal("S-1-5-32-545", AudioConstants.SidUsers);
        Assert.Equal("S-1-3-0", AudioConstants.SidOwnerRights);
    }

    [Fact]
    public void Lista_de_arquivos_base_tem_os_binarios_do_audio()
    {
        Assert.Contains(AudioConstants.SystemFiles, f => f.EndsWith("audiosrv.dll", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(AudioConstants.SystemFiles, f => f.EndsWith("hdaudio.sys", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(7, AudioConstants.SystemFiles.Length);
    }
}
