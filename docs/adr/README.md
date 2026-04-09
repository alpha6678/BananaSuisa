# ADRs

Esta pasta guarda os registros de decisoes arquiteturais do BananaSuisa.

## Quando criar um ADR

Crie um ADR quando uma decisao:

- mudar a stack principal do produto;
- alterar a forma de distribuicao ou empacotamento;
- introduzir uma dependencia estrutural importante;
- definir um padrao que afete varias partes do repositorio;
- impactar a estrategia de migracao para .NET.

## Quando nao criar um ADR

Nao e necessario ADR para:

- correcoes locais de bug;
- ajustes pequenos de UI;
- texto de documentacao sem impacto arquitetural;
- refactors limitados que nao mudam a direcao tecnica.

## Estrutura recomendada

Cada ADR deve seguir o template em [`TEMPLATE.md`](TEMPLATE.md) e incluir:

- Titulo
- Data
- Estado
- Contexto
- Decisao
- Consequencias

## Convencao de nomes

Use o formato:

`ADR-000-formato.md`
`ADR-001-stack-ui.md`
`ADR-002-empacotamento.md`

Os numeros devem ser sequenciais e nunca reaproveitados.

## Primeiro ADR desta pasta

- [`ADR-000-formato.md`](ADR-000-formato.md)
- [`ADR-001-stack-ui.md`](ADR-001-stack-ui.md)
