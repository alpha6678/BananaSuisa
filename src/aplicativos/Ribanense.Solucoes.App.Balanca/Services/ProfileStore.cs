using Ribanense.Solucoes.App.Balanca.Domain;
using Ribanense.Solucoes.PluginSDK.Vault;

namespace Ribanense.Solucoes.App.Balanca.Services;

/// <summary>
/// Persiste, por modelo, a última configuração serial que funcionou, para
/// pré-preencher a interface nas próximas execuções.
/// </summary>
public sealed class ProfileStore
{
    private readonly IVault _vault;

    public ProfileStore(IVault vault)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
    }

    public void Save(string modelKey, SerialConfig config)
    {
        if (string.IsNullOrWhiteSpace(modelKey)) return;
        ArgumentNullException.ThrowIfNull(config);
        _vault.SetSetting(Key(modelKey), config);
    }

    public SerialConfig? TryLoad(string modelKey)
    {
        if (string.IsNullOrWhiteSpace(modelKey)) return null;
        try
        {
            return _vault.GetSetting<SerialConfig>(Key(modelKey));
        }
        catch
        {
            return null;
        }
    }

    private static string Key(string modelKey) => $"profile:{modelKey.ToLowerInvariant()}";
}
