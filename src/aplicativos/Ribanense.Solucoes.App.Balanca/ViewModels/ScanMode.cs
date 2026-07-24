namespace Ribanense.Solucoes.App.Balanca.ViewModels;

/// <summary>Modo de operação da tela de teste de balança.</summary>
public enum ScanMode
{
    /// <summary>Teste manual clássico: usuário configura tudo e aciona a leitura.</summary>
    Manual,

    /// <summary>Varredura passo a passo, pausando a cada combinação testada.</summary>
    UmAUm,

    /// <summary>Varredura completa de todas as combinações, com ranking.</summary>
    Todas,
}
