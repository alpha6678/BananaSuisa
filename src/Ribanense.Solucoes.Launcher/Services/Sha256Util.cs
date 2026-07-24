using System.Security.Cryptography;

namespace Ribanense.Solucoes.Launcher.Services;

/// <summary>
/// Utilitarios de SHA256 compartilhados entre instalacao de apps e auto-atualizacao do launcher.
/// </summary>
public static class Sha256Util
{
    /// <summary>Calcula o hash SHA256 em hexadecimal minusculo.</summary>
    public static string Compute(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
    }

    /// <summary>
    /// Extrai o hash de um arquivo ".sha256" (formato comum: "&lt;hash&gt;  &lt;arquivo&gt;" ou somente "&lt;hash&gt;").
    /// </summary>
    public static string ExtractHash(string shaText)
    {
        string line = shaText.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
            ?? string.Empty;
        string head = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty;
        return head.Trim().ToLowerInvariant();
    }
}
