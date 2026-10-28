namespace Ribanense.Solucoes.App.Recuperacao.Recovery.Audio;

/// <summary>
/// Constantes do subsistema de áudio do Windows extraídas do script de
/// referência: GUIDs de classe, SIDs conhecidos, serviços e arquivos base.
/// </summary>
public static class AudioConstants
{
    /// <summary>GUID da classe MEDIA (dispositivos de áudio).</summary>
    public const string MediaClassGuid = "{4d36e96c-e325-11ce-bfc1-08002be10318}";

    /// <summary>GUID da classe AudioEndpoint.</summary>
    public const string AudioEndpointClassGuid = "{cdf10ab0-12ef-11d0-9d0c-00c04fc9a425}";

    /// <summary>SID de SYSTEM (LocalSystem).</summary>
    public const string SidSystem = "S-1-5-18";

    /// <summary>SID do grupo Administrators.</summary>
    public const string SidAdministrators = "S-1-5-32-544";

    /// <summary>SID do grupo Users (acesso de leitura).</summary>
    public const string SidUsers = "S-1-5-32-545";

    /// <summary>SID de OWNER RIGHTS — quando presente, fecha a DACL contra o próprio dono.</summary>
    public const string SidOwnerRights = "S-1-3-0";

    /// <summary>Serviços essenciais do subsistema de áudio.</summary>
    public static readonly string[] CoreServices = ["Audiosrv", "AudioEndpointBuilder", "RpcSs", "HDAudBus", "hdaudio"];

    /// <summary>Arquivos base de áudio (indicador de imagem removida/enxugada).</summary>
    public static readonly string[] SystemFiles =
    [
        @"%SystemRoot%\System32\audiosrv.dll",
        @"%SystemRoot%\System32\audioeng.dll",
        @"%SystemRoot%\System32\audiodg.exe",
        @"%SystemRoot%\System32\mmres.dll",
        @"%SystemRoot%\System32\SndVolSSO.dll",
        @"%SystemRoot%\System32\drivers\hdaudbus.sys",
        @"%SystemRoot%\System32\drivers\hdaudio.sys",
    ];

    /// <summary>Caminho relativo em HKLM da árvore de endpoints de áudio.</summary>
    public const string MmDevicesAudioPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio";

    /// <summary>Políticas de bloqueio de instalação de dispositivos.</summary>
    public const string DeviceInstallRestrictionsPath = @"SOFTWARE\Policies\Microsoft\Windows\DeviceInstall\Restrictions";

    /// <summary>Chaves de serviço comparadas com o LastKnownGood.</summary>
    public static readonly string[] LastKnownGoodServices = ["Audiosrv", "AudioEndpointBuilder"];

    /// <summary>URL de download da ISO oficial do Windows 11 (guia in-place).</summary>
    public const string Windows11IsoUrl = "https://www.microsoft.com/pt-br/software-download/windows11";

    public const string ServicesRegistryRoot = @"SYSTEM\CurrentControlSet\Services";
}
