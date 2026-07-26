using Ribanense.Solucoes.App.Balanca.Domain;
using Ribanense.Solucoes.App.Balanca.Protocols;
using Ribanense.Solucoes.App.Balanca.Serial;

namespace Ribanense.Solucoes.App.Balanca.Services;

/// <summary>
/// Leitor de peso no modo manual clássico: Ativar, Ler Peso (ou Monitorar) e
/// Desativar mantendo a porta aberta entre leituras.
/// </summary>
public sealed class BalancaReader : IDisposable
{
    private readonly ISerialChannelFactory _factory;
    private ISerialChannel? _channel;
    private IBalancaProtocol? _protocol;
    private SerialConfig? _config;

    public BalancaReader(ISerialChannelFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public bool IsActive => _channel is { IsOpen: true };

    public SerialConfig? CurrentConfig => _config;

    /// <summary>Abre a porta e prepara a leitura. Lança em caso de falha.</summary>
    public void Activate(SerialConfig config, IBalancaProtocol protocol)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(protocol);

        Deactivate();
        var channel = _factory.Create();
        channel.Open(config);
        _channel = channel;
        _protocol = protocol;
        _config = config;
    }

    public void Deactivate()
    {
        _channel?.Dispose();
        _channel = null;
        _protocol = null;
        _config = null;
    }

    public Task<SerialReadOutcome> ReadWeightAsync(CancellationToken ct = default)
    {
        if (_channel is not { IsOpen: true } channel || _protocol is null || _config is null)
            throw new InvalidOperationException("Ative a balança antes de ler o peso.");

        var protocol = _protocol;
        var options = SerialReadOptions.FromConfig(_config);
        return Task.Run(() => SerialWeightReader.Read(channel, protocol, options, ct), ct);
    }

    public void Dispose() => Deactivate();
}
