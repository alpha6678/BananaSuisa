# ADR-000-formato

- Data: 2026-04-09
- Estado: aceite

## Contexto

O projeto esta a construir uma base de documentacao antes do esqueleto .NET e ainda nao tinha um formato explicito para registrar decisoes arquiteturais.

Sem um padrao minimo, decisoes sobre stack de UI, empacotamento, estrategia de integracao com `winget` e automacao podem ficar espalhadas em conversas, commits ou notas soltas.

## Decisao

Adotar uma pasta `docs/adr/` com arquivos numerados sequencialmente e estrutura fixa:

- titulo;
- data;
- estado;
- contexto;
- decisao;
- consequencias;
- referencias opcionais.

O template oficial desta pasta passa a ser [`TEMPLATE.md`](TEMPLATE.md).

## Consequencias

- O projeto ganha um local claro para registar decisoes de medio e longo prazo.
- A futura migracao para .NET pode ser documentada em etapas, sem misturar decisao com implementacao.
- Mudancas estruturais deixam de depender apenas de memoria da equipa ou historico de conversa.

## Referencias

- [`../../README.md`](../../README.md)
- [`../INDICE.md`](../INDICE.md)
- [`TEMPLATE.md`](TEMPLATE.md)
