using Ribanense.Solucoes.App.Balanca.Protocols;

namespace Ribanense.Solucoes.App.Balanca.Domain;

/// <summary>
/// Catálogo de modelos de balança oferecidos ao usuário. Os modelos com formato de
/// protocolo documentado (Toledo, Toledo 2180, Filizola, Urano) apontam para
/// implementações específicas; os demais usam o detector genérico e são marcados como
/// experimentais, pois não têm fixture/manual confirmando o formato.
/// </summary>
public static class BalancaModelRegistry
{
    public static IReadOnlyList<BalancaModel> All { get; } = Build();

    public static BalancaModel Default => All[0];

    public static BalancaModel? FindByKey(string? key) =>
        key is null ? null : All.FirstOrDefault(m => string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<BalancaModel> Build()
    {
        var toledo = new ToledoProtocol();
        var toledo2180 = new Toledo2180Protocol();
        var filizola = new FilizolaProtocol();
        var urano = new UranoProtocol();
        var generic = new GenericHeuristicProtocol();

        const ModelSupport doc = ModelSupport.Documentado;
        const ModelSupport exp = ModelSupport.Experimental;

        return new List<BalancaModel>
        {
            new("automatico", "Automático / Genérico", generic, exp),
            new("filizola", "Filizola", filizola, doc),
            new("toledo", "Toledo (Prix/9094)", toledo, doc),
            new("toledo2180", "Toledo 2180", toledo2180, doc),
            new("urano", "Urano", urano, doc),
            new("uranopop", "Urano POP", urano, doc),
            new("lucastec", "LucasTec", generic, exp),
            new("magna", "Magna", generic, exp),
            new("digitron", "Digitron", generic, exp),
            new("magellan", "Magellan", generic, exp),
            new("lider", "Lider", generic, exp),
            new("simulada", "Balança simulada (demo)", generic, exp, isSimulated: true),
        };
    }
}
