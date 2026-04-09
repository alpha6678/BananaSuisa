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
            return new CatalogSourceResult(name, false, sourcePath, 0, "Catalogo nao encontrado.");
        }

        try
        {
            using JsonDocument json = JsonDocument.Parse(File.ReadAllText(sourcePath));

            if (json.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new CatalogSourceResult(name, false, sourcePath, 0, "O catalogo nao esta em formato de array JSON.");
            }

            int itemCount = json.RootElement.GetArrayLength();
            return new CatalogSourceResult(name, true, sourcePath, itemCount, $"Catalogo carregado com {itemCount} item(ns).");
        }
        catch (Exception ex)
        {
            return new CatalogSourceResult(name, false, sourcePath, 0, $"Falha ao carregar catalogo: {ex.Message}");
        }
    }
}
