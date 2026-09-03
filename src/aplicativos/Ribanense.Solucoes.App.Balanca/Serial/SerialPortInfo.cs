namespace Ribanense.Solucoes.App.Balanca.Serial;

/// <summary>
/// Origem da porta serial, na mesma leitura que o Gerenciador de Dispositivos permite:
/// adaptador USB-serial, porta virtual de link Bluetooth ou COM nativa da placa.
/// </summary>
public enum SerialPortKind
{
    Desconhecida,
    UsbSerial,
    Bluetooth,
    Nativa,
    Simulada,
}

/// <summary>
/// Porta serial detectada, com nome amigável e identidade estável do dispositivo
/// (PNP ID e VID/PID quando disponíveis) para reconhecer o mesmo adaptador mesmo que
/// o número da porta COM mude após reconexão USB.
/// </summary>
/// <remarks>
/// Num checkout é comum haver duas ou três COMs ao mesmo tempo (balança + maquininha
/// TEF). Por isso a porta carrega também sua origem (<see cref="Kind"/>) e se está
/// ocupada por outro programa, para o usuário escolher a certa em vez de tentar todas.
/// </remarks>
public sealed record SerialPortInfo(
    string Port,
    string? FriendlyName,
    string? PnpDeviceId = null,
    string? Vid = null,
    string? Pid = null,
    SerialPortKind Kind = SerialPortKind.Desconhecida,
    bool IsBusy = false)
{
    /// <summary>Rótulo no formato do Gerenciador de Dispositivos, ex.: "USB-SERIAL CH340 (COM5)".</summary>
    public string DeviceManagerLabel =>
        string.IsNullOrWhiteSpace(FriendlyName) ? Port : $"{FriendlyName} ({Port})";

    public string Display => DeviceManagerLabel;

    public bool IsBluetooth => Kind == SerialPortKind.Bluetooth;

    public bool IsUsbSerial => Kind == SerialPortKind.UsbSerial;

    /// <summary>Pista de qual equipamento costuma estar nessa porta, exibida na lista.</summary>
    public string RoleHint => Kind switch
    {
        SerialPortKind.Bluetooth => "Link Bluetooth — normalmente é a maquininha TEF",
        SerialPortKind.UsbSerial => "Adaptador USB-serial — candidato a balança",
        SerialPortKind.Nativa => "Porta COM da placa — candidato a balança",
        SerialPortKind.Simulada => "Balança simulada (sem hardware)",
        _ => "Origem não identificada",
    };

    /// <summary>Status de uso, no espírito do "em uso" do Gerenciador de Dispositivos.</summary>
    public string StatusHint => IsBusy ? "Em uso por outro programa" : "Livre";

    /// <summary>Identidade estável do dispositivo, independente do número da COM.</summary>
    public string StableId => (Vid, Pid) switch
    {
        (not null, not null) => $"VID_{Vid}&PID_{Pid}",
        _ => PnpDeviceId ?? Port,
    };
}
