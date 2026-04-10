namespace BananaSuisa.Core.Winget;

/// <summary>
/// Classifica a origem/tipo de instalação para exibição na UI (distinto do nome da fonte do repositório).
/// </summary>
public static class WingetInstallationOrigin
{
    /// <param name="repositorySource">Valor da coluna "Fonte" / nome do source no JSON (ex.: winget, msstore).</param>
    /// <param name="packageId">PackageIdentifier.</param>
    /// <param name="packageFamilyName">Opcional: campo PackageFamilyName do JSON (lista/pesquisa recente).</param>
    /// <param name="installerType">Opcional: InstallerType do manifesto/JSON quando existir.</param>
    public static string Resolve(
        string? repositorySource,
        string packageId,
        string? packageFamilyName = null,
        string? installerType = null)
    {
        string id = packageId.Trim();
        string? pfn = packageFamilyName?.Trim();
        if (!string.IsNullOrEmpty(pfn))
        {
            return "UWP / MSIX";
        }

        string it = installerType?.Trim() ?? "";
        if (it.Length > 0)
        {
            if (ContainsAny(it, "msix", "appx", "appxbundle"))
            {
                return "UWP / MSIX (AppX)";
            }

            if (it.Contains("msi", StringComparison.OrdinalIgnoreCase))
            {
                return "MSI";
            }

            if (ContainsAny(it, "inno", "nullsoft", "nsis", "exe", "burn", "wix"))
            {
                return "Win32 (EXE)";
            }

            return $"Instalador: {it}";
        }

        // winget list (texto): pacotes da Loja costumam vir como MSIX\Pacote_versao__hash (coluna Origem vazia).
        if (IsMicrosoftStoreMsixPackageId(id))
        {
            return "Microsoft Store / MSIX";
        }

        string s = repositorySource?.Trim() ?? "";
        // Fonte msstore ou texto amigável da Loja (antes de inferir só pelo ID curto 9...).
        if (IsMicrosoftStoreRepositoryLabel(s))
        {
            return "Microsoft Store (UWP/MSIX)";
        }

        // ID de produto no catálogo da Loja (pesquisa winget / listagem sem coluna Origem).
        if (LooksLikeStoreProductId(id))
        {
            return "Microsoft Store (catálogo)";
        }

        if (LooksLikePackageFamilyNameStyle(id))
        {
            return "UWP / MSIX (ID tipo Store)";
        }

        if (string.IsNullOrEmpty(s))
        {
            return "Externo (ARP / fora do catálogo)";
        }

        if (s.Equals("winget", StringComparison.OrdinalIgnoreCase))
        {
            return "Winget (repositório)";
        }

        return $"Winget — {s}";
    }

    /// <summary>
    /// True quando o winget usa prefixo de pacote MSIX/AppX (inclui apps da Loja e MSIX de sistema).
    /// </summary>
    private static bool IsMicrosoftStoreMsixPackageId(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        if (id.StartsWith("MSIX\\", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (id.StartsWith("APPX\\", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// ID curto do catálogo da Loja (ex.: 9WZDNCRFJ3PS), usado em pesquisas e em algumas entradas.
    /// </summary>
    private static bool LooksLikeStoreProductId(string id)
    {
        if (id.Length is < 12 or > 14)
        {
            return false;
        }

        if (id[0] != '9')
        {
            return false;
        }

        foreach (char c in id)
        {
            if (!char.IsLetterOrDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsMicrosoftStoreRepositoryLabel(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return false;
        }

        if (s.Equals("msstore", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (s.Contains("msstore", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (s.Contains("Microsoft Store", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Locale PT: coluna "Origem" pode mostrar nome amigável da Loja.
        if (s.Contains("Loja", StringComparison.OrdinalIgnoreCase) &&
            (s.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) || s.Contains("Windows", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static bool ContainsAny(string haystack, params string[] needles)
    {
        foreach (string n in needles)
        {
            if (haystack.Contains(n, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikePackageFamilyNameStyle(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        if (id.Contains('!', StringComparison.Ordinal))
        {
            return true;
        }

        if (id.EndsWith("8wekyb3d8bbwe", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
