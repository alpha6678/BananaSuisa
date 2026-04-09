# AGENTS

Este arquivo orienta agentes de IA que trabalhem no repositório `BananaSuisa/`.

## Objetivo do projeto

O BananaSuisa é uma aplicação desktop Windows (C# WPF), focada em instalacao, atualizacao, remocao, reparo e operacoes auxiliares ligadas ao ecossistema `winget`.

O código-fonte base fica na solução `.slnx` e dentro da pasta `src/`.

## Mapa rapido do codigo

| Caminho | Papel |
|---------|-------|
| `src/BananaSuisa.App/` | App WPF e views. |
| `src/BananaSuisa.Core/` | Entidades e modelos. |
| `src/BananaSuisa.Services/` | Lógica de negócios. |
| `src/BananaSuisa.Infrastructure/` | Implementação de dependências externas (como o `winget`). |
| `src/BananaSuisa.Shared/` | Contratos e tipos comuns. |
| `BananaSuisa_recursos/` | Configuracoes base, catalogos, instaladores e memoria persistida. |
| `ferramentas/` | CLI de desenvolvimento. |
| `docs/` | Documentacao de processo, ambiente, IA e referencias. |

## Como trabalhar neste repositorio

- Responder em pt-BR.
- Fazer mudancas pequenas e localizadas sempre que possivel.
- Se tocar em instalacao, remocao, reparo, drivers, cache ou paths de dados, considerar teste manual com privilegios elevados, já que o .exe pede UAC.
- Atualizar documentacao quando comportamento, requisitos ou comandos mudarem.

## Comandos uteis

```bat
.\bs.cmd help
.\bs.cmd compilar
.\bs.cmd run
.\bs.cmd test
.\bs.cmd check
```

## Quando usar subagentes

- Use um agente de exploracao para mapear areas amplas do repositorio, localizar funcoes ou resumir grupos de ficheiros.
- Use um agente de shell para fluxos de build, validacao, git ou comandos multi-etapa.
- Use um agente geral quando a tarefa exigir pesquisa e edicao em varias etapas.

## Validacao esperada

- Para documentacao: revisar links e coerencia com os ficheiros reais.
- Para mudancas na base .NET: usar `.\bs.cmd compilar`, `.\bs.cmd test` ou `.\bs.cmd check`.
- Para mudancas de runtime `winget` ou UI desktop: indicar claramente se ainda falta validacao manual no Windows.

## Documentacao de apoio

- [`README.md`](README.md)
- [`CONTRIBUTING.md`](CONTRIBUTING.md)
- [`docs/INDICE.md`](docs/INDICE.md)
- [`docs/AMBIENTE.md`](docs/AMBIENTE.md)
- [`docs/FERRAMENTAS_CLI.md`](docs/FERRAMENTAS_CLI.md)
- [`docs/FERRAMENTAS_IA.md`](docs/FERRAMENTAS_IA.md)
