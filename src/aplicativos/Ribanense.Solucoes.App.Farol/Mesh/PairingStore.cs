using System.Security.Cryptography;
using System.Text;
using Ribanense.Solucoes.PluginSDK.Vault;

namespace Ribanense.Solucoes.App.Farol.Mesh;

/// <summary>
/// Pareamento por código da loja. Só faróis que compartilham o mesmo código
/// trocam dossiês.
/// </summary>
/// <remarks>
/// O código nunca vai para a rede em claro: o beacon UDP carrega apenas o hash,
/// e o servidor de pares recusa qualquer requisição cujo hash não bata. Sem isso
/// qualquer máquina numa rede compartilhada leria o inventário das outras.
/// </remarks>
public sealed class PairingStore
{
    private const string StoreCodeKey = "mesh.storeCode";
    private const string MachineIdKey = "mesh.machineId";
    private const string FriendlyNameKey = "mesh.friendlyName";
    private const string MeshEnabledKey = "mesh.enabled";

    // Sal fixo do produto: o objetivo é impedir leitura casual do código na
    // rede, não resistir a um atacante com o binário em mãos.
    private static readonly byte[] Salt = Encoding.UTF8.GetBytes("ribanense-farol-v1");

    private readonly IVault _vault;

    public PairingStore(IVault vault)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
    }

    public string? StoreCode
    {
        get
        {
            string? value = _vault.GetSetting(StoreCodeKey);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }

    public bool IsPaired => StoreCode is not null;

    public bool MeshEnabled
    {
        get => !string.Equals(_vault.GetSetting(MeshEnabledKey), "false", StringComparison.OrdinalIgnoreCase);
        set => _vault.SetSetting(MeshEnabledKey, value ? "true" : "false");
    }

    /// <summary>Identificador estável desta instalação, gerado uma única vez.</summary>
    public string MachineId
    {
        get
        {
            string? existing = _vault.GetSetting(MachineIdKey);
            if (!string.IsNullOrWhiteSpace(existing)) return existing;

            string generated = Guid.NewGuid().ToString("N");
            _vault.SetSetting(MachineIdKey, generated);
            return generated;
        }
    }

    public string FriendlyName
    {
        get
        {
            string? value = _vault.GetSetting(FriendlyNameKey);
            return string.IsNullOrWhiteSpace(value) ? Environment.MachineName : value;
        }
        set => _vault.SetSetting(FriendlyNameKey, Normalize(value) ?? Environment.MachineName);
    }

    public string? StoreCodeHash => StoreCode is null ? null : Hash(StoreCode);

    public void Pair(string storeCode)
    {
        string? normalized = Normalize(storeCode);
        if (normalized is null)
            throw new ArgumentException("Código da loja obrigatório.", nameof(storeCode));

        _vault.SetSetting(StoreCodeKey, normalized);
    }

    public void Unpair() => _vault.RemoveSetting(StoreCodeKey);

    public bool Accepts(string? incomingHash) =>
        StoreCodeHash is { } mine
        && !string.IsNullOrWhiteSpace(incomingHash)
        && CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(mine),
            Encoding.ASCII.GetBytes(incomingHash));

    /// <summary>Hash do código, normalizado para o usuário poder digitar como quiser.</summary>
    public static string Hash(string storeCode)
    {
        byte[] input = Encoding.UTF8.GetBytes(Canonical(storeCode));
        byte[] digest = SHA256.HashData([.. Salt, .. input]);
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    internal static string Canonical(string storeCode) =>
        storeCode.Trim().ToUpperInvariant().Replace(" ", string.Empty);

    private static string? Normalize(string? value)
    {
        string trimmed = (value ?? string.Empty).Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
