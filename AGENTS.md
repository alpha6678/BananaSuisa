# AGENTS

Este arquivo orienta agentes de IA que trabalhem no repositório `BananaSuisa/`.

## Objetivo do projeto

O BananaSuisa e uma aplicacao desktop Windows baseada em PowerShell + WinForms, focada em instalacao, atualizacao, remocao, reparo e operacoes auxiliares ligadas ao ecossistema `winget`.

O codigo-fonte editavel vive em `BananaSuisa_desenvolvimento/`. O arquivo `BananaSuisa.ps1` na raiz e apenas o consolidado gerado.

## Regra mais importante

Nunca trate `BananaSuisa.ps1` como fonte principal. Edite os modulos em `BananaSuisa_desenvolvimento/` e gere o consolidado com `.\bs.cmd build`.

## Mapa rapido do codigo

| Caminho | Papel |
|---------|-------|
| `BananaSuisa_desenvolvimento/nucleo/` | Bootstrap, requisitos, configuracao, workspace, logs e deteccao de `winget`. |
| `BananaSuisa_desenvolvimento/interface/` | Tema, layout e views WinForms. |
| `BananaSuisa_desenvolvimento/funcionalidades/` | Busca, catalogo, downloads, drivers, cache e acoes do produto. |
| `BananaSuisa_desenvolvimento/eventos/` | Wiring de eventos da UI e execucao dos modos. |
| `BananaSuisa_recursos/` | Configuracoes base, catalogos, instaladores e memoria persistida. |
| `ferramentas/` | Build consolidado e CLI de desenvolvimento. |
| `docs/` | Documentacao de processo, ambiente, IA e referencias. |

## Ordem de carga atual

O consolidado segue a ordem definida em `ferramentas/Gerar_BananaSuisa.ps1`:

1. `nucleo/bootstrap.ps1`
2. `interface/theme.ps1`
3. `funcionalidades/search.ps1`
4. `interface/layout.ps1`
5. `funcionalidades/catalog.ps1`
6. `interface/views.ps1`
7. `funcionalidades/actions.ps1`
8. `eventos/app.events.ps1`

Se uma alteracao introduzir novo modulo, dependencia de ordem ou bootstrap adicional, atualize esta lista, o gerador e a documentacao associada.

## Como trabalhar neste repositorio

- Responder em pt-BR.
- Fazer mudancas pequenas e localizadas sempre que possivel.
- Preservar a separacao entre `nucleo`, `interface`, `funcionalidades` e `eventos`.
- Nao remover ou sobrescrever alteracoes do utilizador sem pedido explicito.
- Se tocar em instalacao, remocao, reparo, drivers, cache ou paths de dados, considerar teste manual com privilegios elevados.
- Atualizar documentacao quando comportamento, requisitos ou comandos mudarem.

## Comandos uteis

```bat
.\bs.cmd help
.\bs.cmd versao
.\bs.cmd build
```

Alternativa direta:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\ferramentas\Gerar_BananaSuisa.ps1
```

## Quando usar subagentes

- Use um agente de exploracao para mapear areas amplas do repositorio, localizar funcoes ou resumir grupos de ficheiros.
- Use um agente de shell para fluxos de build, validacao, git ou comandos multi-etapa.
- Use um agente geral quando a tarefa exigir pesquisa e edicao em varias etapas.

## Validacao esperada

- Para documentacao: revisar links e coerencia com os ficheiros reais.
- Para mudancas de script: gerar o consolidado com `.\bs.cmd build`.
- Para mudancas de runtime `winget` ou UI desktop: indicar claramente se ainda falta validacao manual no Windows.

## Documentacao de apoio

- [`README.md`](README.md)
- [`CONTRIBUTING.md`](CONTRIBUTING.md)
- [`docs/INDICE.md`](docs/INDICE.md)
- [`docs/AMBIENTE.md`](docs/AMBIENTE.md)
- [`docs/FERRAMENTAS_CLI.md`](docs/FERRAMENTAS_CLI.md)
- [`docs/FERRAMENTAS_IA.md`](docs/FERRAMENTAS_IA.md)
