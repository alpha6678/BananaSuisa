using Ribanense.Solucoes.App.Winget.Services.Search;
using Xunit;

namespace Ribanense.Solucoes.App.Winget.Tests;

public class EmbeddedAppAliasCatalogTests
{
    [Fact]
    public void Loads_at_least_20_entries()
    {
        var catalog = new EmbeddedAppAliasCatalog();
        Assert.True(catalog.All.Count >= 50, $"esperava >=50, obtido {catalog.All.Count}");
    }

    [Fact]
    public void Contains_vscode_and_chrome()
    {
        var catalog = new EmbeddedAppAliasCatalog();
        Assert.Contains(catalog.All, a => a.Id == "Microsoft.VisualStudioCode");
        Assert.Contains(catalog.All, a => a.Id == "Google.Chrome");
    }

    [Fact]
    public void Each_entry_has_id_and_synonyms()
    {
        var catalog = new EmbeddedAppAliasCatalog();
        Assert.All(catalog.All, a =>
        {
            Assert.False(string.IsNullOrWhiteSpace(a.Id), $"id vazio para: {a.PublicName}");
            Assert.NotNull(a.Synonyms);
        });
    }

    [Fact]
    public void Synonyms_are_not_empty_strings()
    {
        var catalog = new EmbeddedAppAliasCatalog();
        foreach (var alias in catalog.All)
        {
            foreach (var syn in alias.Synonyms)
            {
                Assert.False(string.IsNullOrWhiteSpace(syn), $"sinonimo vazio em {alias.Id}");
            }
        }
    }

    [Fact]
    public void Suggested_contains_required_audited_apps()
    {
        var catalog = new EmbeddedAppAliasCatalog();
        string[] required =
        [
            "Google.Chrome",
            "Mozilla.Firefox",
            "VideoLAN.VLC",
            "MPC-BE.MPC-BE",
            "AnyDesk.AnyDesk",
            "TeamViewer.TeamViewer",
            "Microsoft.Teams",
            "TheDocumentFoundation.LibreOffice",
            "OBSProject.OBSStudio"
        ];

        foreach (string id in required)
        {
            var alias = Assert.Single(catalog.All, a => a.Id == id);
            Assert.True(alias.IsSuggested, $"{id} deveria estar marcado como sugerido");
            Assert.Equal("verified-winget-show", alias.AuditStatus);
            Assert.Contains(catalog.Suggested, a => a.Id == id);
        }
    }

    [Fact]
    public void Suggested_are_ordered_by_suggested_order()
    {
        var catalog = new EmbeddedAppAliasCatalog();
        var orders = catalog.Suggested.Select(a => a.SuggestedOrder ?? int.MaxValue).ToList();

        Assert.NotEmpty(orders);
        Assert.Equal(orders.OrderBy(v => v), orders);
    }
}
