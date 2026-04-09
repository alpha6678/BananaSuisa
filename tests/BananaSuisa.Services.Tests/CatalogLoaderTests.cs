using BananaSuisa.Core.Catalog;
using BananaSuisa.Core.Workspace;
using BananaSuisa.Infrastructure.Catalog;
using BananaSuisa.Services.Search;

namespace BananaSuisa.Services.Tests;

public sealed class CatalogLoaderTests : IDisposable
{
    private readonly string _projectRoot;
    private readonly WorkspacePaths _paths;

    public CatalogLoaderTests()
    {
        _projectRoot = Path.Combine(Path.GetTempPath(), "BananaSuisa.Tests", Guid.NewGuid().ToString("N"));
        _paths = WorkspacePaths.FromProjectRoot(_projectRoot);

        Directory.CreateDirectory(_paths.DataRoot);
        Directory.CreateDirectory(_paths.ResourcesRoot);
    }

    [Fact]
    public void Load_ParsesAliasesAndKeepsOnlyCurrentCatalogSources()
    {
        File.WriteAllText(_paths.InstallCatalogPath, """
[
  { "name": "Chrome Corporativo", "id": "Google.Chrome", "category": "Navegadores", "essential": true },
  { "N": "ERP Cliente", "I": "Empresa.ERP", "C": "Negocio", "E": false },
  "WinRAR.WinRAR"
]
""");
        File.WriteAllText(_paths.TechCatalogPath, """
[
  { "Name": "Chrome Duplicado", "Id": "Google.Chrome", "Category": "Tecnico", "Essential": false }
]
""");

        CatalogLoader loader = new();

        CatalogLoadResult result = loader.Load(_paths);

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.UniqueItemCount);
        Assert.Equal(2, result.Sources.Count);
        Assert.DoesNotContain(result.Sources, source => source.Name == "Legado");
        Assert.Contains(result.AllItems, item => item.PackageId == "Empresa.ERP" && item.Name == "ERP Cliente" && item.Category == "Negocio");
        Assert.Contains(result.AllItems, item => item.PackageId == "WinRAR.WinRAR" && item.Name == "WinRAR.WinRAR");

        CatalogItem chrome = Assert.Single(result.AllItems, item => item.PackageId == "Google.Chrome");
        Assert.Equal("Chrome Corporativo", chrome.Name);
    }

    [Fact]
    public void Search_FindsTypedCatalogEntries()
    {
        File.WriteAllText(_paths.InstallCatalogPath, """
[
  { "Name": "Chrome Corporativo", "Id": "Google.Chrome", "Category": "Navegadores", "Essential": true },
  { "N": "ERP Cliente", "I": "Empresa.ERP", "C": "Negocio", "E": false },
  "WinRAR.WinRAR"
]
""");
        File.WriteAllText(_paths.TechCatalogPath, "[]");

        CatalogLoader loader = new();
        CatalogSearchService searchService = new();

        CatalogLoadResult result = loader.Load(_paths);
        IReadOnlyList<CatalogItem> erpMatches = searchService.Search(result, "erp");
        CatalogSearchPreview preview = searchService.BuildPreview(result);

        Assert.Contains(erpMatches, item => item.PackageId == "Empresa.ERP");
        Assert.Equal(3, preview.UniqueItemCount);
        Assert.Equal(3, preview.CategoryCount);
        Assert.Equal(1, preview.EssentialItemCount);
        Assert.NotEmpty(preview.PreviewItems);
    }

    [Fact]
    public void BuildPreview_AllowsEmptyCatalogWithoutFallbackSeed()
    {
        File.WriteAllText(_paths.InstallCatalogPath, "[]");
        File.WriteAllText(_paths.TechCatalogPath, "[]");

        CatalogLoader loader = new();
        CatalogSearchService searchService = new();

        CatalogLoadResult result = loader.Load(_paths);
        CatalogSearchPreview preview = searchService.BuildPreview(result);

        Assert.True(result.Succeeded);
        Assert.Empty(result.AllItems);
        Assert.Equal(0, preview.UniqueItemCount);
        Assert.Equal(string.Empty, preview.PreviewQuery);
        Assert.Empty(preview.PreviewItems);
    }

    public void Dispose()
    {
        if (Directory.Exists(_projectRoot))
        {
            Directory.Delete(_projectRoot, recursive: true);
        }
    }
}
