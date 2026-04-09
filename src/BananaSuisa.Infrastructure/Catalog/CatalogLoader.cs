using System.Text.Json;
using BananaSuisa.Core.Catalog;
using BananaSuisa.Core.Workspace;
using BananaSuisa.Services.Abstractions;

namespace BananaSuisa.Infrastructure.Catalog;

public sealed class CatalogLoader : ICatalogLoader
{
    public CatalogLoadResult Load(WorkspacePaths paths)
    {
        List<CatalogSourceResult> results =
        [
            LoadSingle("Instalacao", paths.InstallCatalogPath, paths.PayloadInstallCatalogPath),
            LoadSingle("Tecnico", paths.TechCatalogPath, paths.PayloadTechCatalogPath)
        ];

        return new CatalogLoadResult(results);
    }

    private static CatalogSourceResult LoadSingle(string name, string primaryPath, string fallbackPath)
    {
        string sourcePath = File.Exists(primaryPath) ? primaryPath : fallbackPath;
        if (!File.Exists(sourcePath))
        {
            return new CatalogSourceResult(name, false, sourcePath, 0, "Catalogo nao encontrado.", []);
        }

        try
        {
            using JsonDocument json = JsonDocument.Parse(File.ReadAllText(sourcePath));

            if (json.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new CatalogSourceResult(name, false, sourcePath, 0, "O catalogo nao esta em formato de array JSON.", []);
            }

            CatalogItem[] items = json.RootElement
                .EnumerateArray()
                .Select(element => ParseItem(element, name))
                .Where(item => item is not null)
                .Cast<CatalogItem>()
                .ToArray();

            return new CatalogSourceResult(name, true, sourcePath, items.Length, $"Catalogo carregado com {items.Length} item(ns).", items);
        }
        catch (Exception ex)
        {
            return new CatalogSourceResult(name, false, sourcePath, 0, $"Falha ao carregar catalogo: {ex.Message}", []);
        }
    }

    private static CatalogItem? ParseItem(JsonElement element, string sourceName)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => ParseStringItem(element, sourceName),
            JsonValueKind.Object => ParseObjectItem(element, sourceName),
            _ => null
        };
    }

    private static CatalogItem? ParseStringItem(JsonElement element, string sourceName)
    {
        string? value = element.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return new CatalogItem(
            Name: value,
            PackageId: value,
            Category: sourceName,
            IsEssential: false,
            SourceName: sourceName);
    }

    private static CatalogItem? ParseObjectItem(JsonElement element, string sourceName)
    {
        string packageId = GetString(element, "id", "Id", "I", "packageId", "PackageId", "wingetId", "WingetId", "winget_id", "PackageIdentifier");
        string name = GetString(element, "name", "Name", "N", "title", "Title", "displayName", "DisplayName");
        string category = GetString(element, "category", "Category", "C");
        bool isEssential = GetBool(element, "essential", "Essential", "E", "isEssential", "IsEssential");

        if (string.IsNullOrWhiteSpace(packageId) && string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        string resolvedPackageId = string.IsNullOrWhiteSpace(packageId) ? name : packageId;
        string resolvedName = string.IsNullOrWhiteSpace(name) ? resolvedPackageId : name;
        string resolvedCategory = string.IsNullOrWhiteSpace(category) ? sourceName : category;

        return new CatalogItem(
            Name: resolvedName,
            PackageId: resolvedPackageId,
            Category: resolvedCategory,
            IsEssential: isEssential,
            SourceName: sourceName);
    }

    private static string GetString(JsonElement element, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String)
            {
                string? text = value.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return string.Empty;
    }

    private static bool GetBool(JsonElement element, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (value.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            if (value.ValueKind == JsonValueKind.String &&
                bool.TryParse(value.GetString(), out bool parsed))
            {
                return parsed;
            }
        }

        return false;
    }
}
