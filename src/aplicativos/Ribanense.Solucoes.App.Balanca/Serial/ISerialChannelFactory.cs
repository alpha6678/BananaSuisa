using Ribanense.Solucoes.App.Balanca.Domain;

namespace Ribanense.Solucoes.App.Balanca.Serial;

/// <summary>
/// Cria canais seriais. Permite trocar entre porta real e balança simulada
/// conforme o modelo selecionado.
/// </summary>
public interface ISerialChannelFactory
{
    ISerialChannel Create();

    /// <summary>Portas disponíveis para este canal (reais ou virtuais).</summary>
    IReadOnlyList<SerialPortInfo> ListPorts();
}

/// <summary>Fábrica de canais seriais reais (System.IO.Ports).</summary>
public sealed class RealSerialChannelFactory : ISerialChannelFactory
{
    public ISerialChannel Create() => new SerialPortChannel();

    public IReadOnlyList<SerialPortInfo> ListPorts() => SerialPortEnumerator.Enumerate();
}

/// <summary>Fábrica de balança simulada para o modo demo/testes.</summary>
public sealed class SimulatedSerialChannelFactory : ISerialChannelFactory
{
    public const string SimulatedPort = "COM-SIM";

    private readonly SerialConfig _target;
    private readonly decimal _weight;

    public SimulatedSerialChannelFactory(SerialConfig? target = null, decimal weight = 5.250m)
    {
        _target = target ?? SerialConfig.Default(SimulatedPort);
        _weight = weight;
    }

    public ISerialChannel Create() => new SimulatedSerialChannel(_target, _weight);

    public IReadOnlyList<SerialPortInfo> ListPorts() =>
        new[] { new SerialPortInfo(SimulatedPort, "Balança simulada") };
}
