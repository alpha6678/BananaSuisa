using Ribanense.Solucoes.Infrastructure.Vault;

namespace Ribanense.Solucoes.Infrastructure.Tests;

/// <summary>
/// Constrói um LiteDbVault temporário e remove o arquivo ao descartar.
/// </summary>
public sealed class VaultFixture : IDisposable
{
    private readonly string _path;

    public LiteDbVault Vault { get; }

    public VaultFixture()
    {
        string dir = Path.Combine(Path.GetTempPath(), "ribanense-vault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "test.dat");
        Vault = new LiteDbVault(_path);
    }

    public void Dispose()
    {
        Vault.Dispose();
        try
        {
            string? dir = Path.GetDirectoryName(_path);
            if (dir != null && Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // best effort
        }
    }
}
