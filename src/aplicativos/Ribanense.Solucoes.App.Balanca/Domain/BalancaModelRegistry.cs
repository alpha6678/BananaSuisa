using Ribanense.Solucoes.App.Balanca.Protocols;

namespace Ribanense.Solucoes.App.Balanca.Domain;

/// <summary>
/// Catálogo de modelos de balança oferecidos ao usuário. Mantém os modelos da
/// interface original do ACBrBAL, mapeando cada um para um protocolo preciso
/// (Toledo/Filizola/Urano) ou para o detector genérico.
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
        var filizola = new FilizolaProtocol();
        var urano = new UranoProtocol();
        var generic = new GenericHeuristicProtocol();

        return new List<BalancaModel>
        {
            new("automatico", "Automático / Genérico", generic),
            new("filizola", "Filizola", filizola),
            new("toledo", "Toledo", toledo),
            new("toledo2180", "Toledo 2180", toledo),
            new("urano", "Urano", urano),
            new("uranopop", "Urano POP", urano),
            new("lucastec", "LucasTec", generic),
            new("magna", "Magna", generic),
            new("digitron", "Digitron", generic),
            new("magellan", "Magellan", generic),
            new("lider", "Lider", generic),
            new("simulada", "Balança simulada (demo)", generic, isSimulated: true),
        };
    }
}
