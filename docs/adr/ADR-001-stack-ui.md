# ADR-001-stack-ui

- Data: 2026-04-09
- Estado: aceite

## Contexto

O BananaSuisa vai iniciar a migracao de PowerShell + WinForms para .NET e precisa definir a stack de UI antes da criacao da solution e dos primeiros projetos.

As duas opcoes principais consideradas nesta fase foram:

- `WPF`
- `WinUI 3`

O produto atual:

- e estritamente desktop Windows;
- tem UI tradicional de aplicacao interna;
- depende fortemente de operacoes longas, logs visiveis e integracao com sistema;
- precisa migrar por etapas sem aumentar demais o custo inicial.

## Decisao

Adotar **WPF em .NET 10** como stack da primeira implementacao .NET do BananaSuisa.

## Consequencias

### Vantagens

- Menor friccao para abrir o esqueleto inicial.
- Stack madura, estavel e bem documentada para desktop Windows.
- Menor custo de entrada do que WinUI 3 para a primeira etapa da migracao.
- Boa adequacao a uma aplicacao corporativa com layout, logs, listas, comandos e binding.
- Permite separar mais rapidamente UI, servicos e infraestrutura, que e o foco principal desta fase.

### Trade-offs

- A UI nao nasce na stack visual mais moderna do ecossistema Windows.
- Pode ser necessario rever esta decisao no futuro se houver exigencia forte de componentes ou recursos exclusivos do Windows App SDK.
- Padroes de layout com `ScrollViewer` e controlos com scroll interno (`DataGrid`, `TextBox`) exigem atencao ao encaminhamento da roda do rato; o repositorio documenta convencoes em `src/BananaSuisa.App/Behaviors/README.md` e [`../MELHORIAS.md`](../MELHORIAS.md).

### O que esta fora desta decisao

- Empacotamento final (`.exe`, `.msi`) fica para ADR propria.
- Estrategia definitiva de integracao com `winget` fica para ADR propria.
- Design visual final da interface nao esta fechado aqui.

## Referencias

- [`../../ROADMAP_MIGRACAO.md`](../../ROADMAP_MIGRACAO.md)
- [`../../MAPEAMENTO_PS1_PARA_DOTNET.md`](../../MAPEAMENTO_PS1_PARA_DOTNET.md)
- [`TEMPLATE.md`](TEMPLATE.md)
