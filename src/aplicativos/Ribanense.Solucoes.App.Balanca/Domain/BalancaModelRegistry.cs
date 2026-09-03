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

        const string genericNote =
            "Sem formato confirmado: usa o detector genérico. Se a configuração sugerida não responder, teste as outras combinações conhecidas.";

        return new List<BalancaModel>
        {
            new("automatico", "Automático / Genérico", generic, exp,
                notes: "Use quando a marca não estiver identificada na balança. Começa em 9600 8N1 e reconhece frames STX ou texto com decimal."),
            new("filizola", "Filizola", filizola, doc,
                notes: "Host envia ENQ e a balança responde STX PPPPP ETX. Padrão de fábrica 9600 8N1."),
            new("toledo", "Toledo (Prix/9094)", toledo, doc,
                notes: "Protocolo P05A/Prt3: ENQ → STX PPPPP ETX, peso com 3 casas implícitas. Padrão 9600 8N1."),
            new("toledo2180", "Toledo 2180", toledo2180, doc,
                notes: "Linha terminada em CR com marcador 0x60 e 6 dígitos. Padrão 9600 8N1."),
            new("urano", "Urano", urano, doc,
                notes: "Aceita frame STX/ETX ou texto \"PESO: x,yz kg\". Atenção: o padrão documentado é 9600 8N2 (dois stop bits)."),
            new("uranopop", "Urano POP", urano, doc,
                notes: "Mesmo protocolo da linha Urano, também em 9600 8N2 (dois stop bits)."),
            new("lucastec", "LucasTec", generic, exp, notes: genericNote),
            new("magna", "Magna", generic, exp, notes: genericNote),
            new("digitron", "Digitron", generic, exp, notes: genericNote),
            new("magellan", "Magellan", generic, exp, notes: genericNote),
            new("lider", "Lider", generic, exp, notes: genericNote),
            new("simulada", "Balança simulada (demo)", generic, exp, isSimulated: true,
                notes: "Balança virtual para conferir o app sem hardware conectado."),
        };
    }
}
