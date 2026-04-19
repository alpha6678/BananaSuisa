namespace Ribanense.Solucoes.App.Winget.Services.Search;

public interface IAppAliasCatalog
{
    IReadOnlyList<AppAlias> All { get; }
}
