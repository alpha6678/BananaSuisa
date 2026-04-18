using System.Text.RegularExpressions;

namespace Ribanense.Solucoes.PluginSDK.Manifest;

/// <summary>
/// Validação leve de SemVer 2.0, aceitando x.y.z e x.y.z-pre.tag.
/// Não é um parser completo, apenas checa formato aceitável para os
/// campos version e minimumLauncherVersion do manifesto.
/// </summary>
public static class SemVerLoose
{
    private static readonly Regex Pattern = new(
        @"^\d+\.\d+\.\d+(?:-[0-9A-Za-z\-\.]+)?(?:\+[0-9A-Za-z\-\.]+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool IsValid(string version) =>
        !string.IsNullOrWhiteSpace(version) && Pattern.IsMatch(version.Trim());

    /// <summary>
    /// Compara duas versões. Retorna &lt;0, 0 ou &gt;0 como <see cref="IComparable"/>.
    /// Pre-releases (ex.: 1.0.0-beta.1) são sempre menores que a versão final (1.0.0).
    /// </summary>
    public static int Compare(string left, string right)
    {
        if (!IsValid(left)) throw new ArgumentException($"SemVer inválido: {left}", nameof(left));
        if (!IsValid(right)) throw new ArgumentException($"SemVer inválido: {right}", nameof(right));

        (int[] core, string? pre) l = Split(left);
        (int[] core, string? pre) r = Split(right);

        for (int i = 0; i < 3; i++)
        {
            int c = l.core[i].CompareTo(r.core[i]);
            if (c != 0) return c;
        }

        if (l.pre is null && r.pre is null) return 0;
        if (l.pre is null) return 1;
        if (r.pre is null) return -1;

        return string.Compare(l.pre, r.pre, StringComparison.Ordinal);
    }

    private static (int[] core, string? pre) Split(string version)
    {
        version = version.Trim();
        int plus = version.IndexOf('+');
        if (plus >= 0) version = version[..plus];

        int dash = version.IndexOf('-');
        string coreStr = dash >= 0 ? version[..dash] : version;
        string? pre = dash >= 0 ? version[(dash + 1)..] : null;

        string[] parts = coreStr.Split('.');
        int[] core = new int[3];
        for (int i = 0; i < 3; i++)
        {
            core[i] = int.Parse(parts[i], System.Globalization.CultureInfo.InvariantCulture);
        }
        return (core, pre);
    }
}
