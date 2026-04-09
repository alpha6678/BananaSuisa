# BananaSuisa

## Onde fica cada coisa

| Local | Função |
|-------|--------|
| [`BananaSuisa.slnx`](BananaSuisa.slnx) | Solution da migração completa para .NET (WPF). |
| [`src/`](src/) | Projetos `BananaSuisa.App`, `Core`, `Services`, `Infrastructure` e `Shared`. |
| [`BananaSuisa_recursos/`](BananaSuisa_recursos/) | Modelos (JSON, config) na raiz desta pasta; dados de execução em `BananaSuisa_recursos\BananaSuisa_memoria\`. |
| [`bs.cmd`](bs.cmd) / [`ferramentas/BananaSuisa.cmd`](ferramentas/BananaSuisa.cmd) | CLI de desenvolvimento: `build`, `run`, `test`, `check`, `help` (ver [`docs/FERRAMENTAS_CLI.md`](docs/FERRAMENTAS_CLI.md)). |
| [`.cursor/mcp.json`](.cursor/mcp.json) | Configuração MCP partilhada do projeto. |

## Memória (`BananaSuisa_recursos\BananaSuisa_memoria`)

Tudo fica **no projeto**, dentro de uma subpasta de recursos — não usa `%LOCALAPPDATA%\BananaSuisa_memoria` para o estado do app (na primeira execução após atualizar, dados antigos podem ser **movidos** para cá).

- **Registros:** `BananaSuisa.json` (log no painel) fica em `...\BananaSuisa_memoria\Registros\`.
- Outras subpastas: `Dados`, `Perfis`, `ScriptsExtras`, `Temporarios`, `DriversImpressoras`, `PacotesBaixados` (e `PacotesBaixados\WinGet`). Veja `LEIA-ME.txt` dentro de `BananaSuisa_memoria`.

Apagar a pasta `BananaSuisa_memoria` (com o app fechado) redefine o app aos padrões dos modelos em `BananaSuisa_recursos`.

## Build e Execução

Comandos simples pela CLI:

```bat
.\bs.cmd compilar
.\bs.cmd run
.\bs.cmd test
.\bs.cmd check
```

Equivalentes diretos com `dotnet`:

```powershell
dotnet build .\BananaSuisa.slnx
dotnet run --project .\src\BananaSuisa.App\BananaSuisa.App.csproj
dotnet test .\BananaSuisa.slnx
```

## Documentacao central

- [`docs/INDICE.md`](docs/INDICE.md) — ponto de entrada para toda a documentacao do projeto
- [`CONTRIBUTING.md`](CONTRIBUTING.md) — fluxo de contribuicao, validacao minima e regras praticas
- [`docs/AMBIENTE.md`](docs/AMBIENTE.md) — requisitos atuais do ambiente e setup inicial
- [`AGENTS.md`](AGENTS.md) — contexto e regras para agentes de IA neste repositorio
- [`docs/ROADMAP_MIGRACAO.md`](docs/ROADMAP_MIGRACAO.md) — fases e estrategia da migracao para .NET
- [`docs/MAPEAMENTO_PS1_PARA_DOTNET.md`](docs/MAPEAMENTO_PS1_PARA_DOTNET.md) — mapa de modulos atuais para a estrutura futura

## Documentação extra

- [`docs/FERRAMENTAS_CLI.md`](docs/FERRAMENTAS_CLI.md) — CLI do projeto (`bs.cmd`), winget, git, Node, dotnet
- [`docs/FERRAMENTAS_IA.md`](docs/FERRAMENTAS_IA.md) — MCPs (Playwright, browser), testes e limitações da UI desktop
- [`docs/REFERENCIAS_EXTERNAS.md`](docs/REFERENCIAS_EXTERNAS.md) — links oficiais e bibliotecas relevantes
- [`BananaSuisa_desenvolvimento/docs/ARQUITETURA.md`](BananaSuisa_desenvolvimento/docs/ARQUITETURA.md) — arquitetura atual e ponte para a migracao
- [`BananaSuisa_desenvolvimento/docs/FLUXO_INSTALACAO.md`](BananaSuisa_desenvolvimento/docs/FLUXO_INSTALACAO.md)
- [`BananaSuisa_desenvolvimento/docs/SOLUCAO_PROBLEMAS.md`](BananaSuisa_desenvolvimento/docs/SOLUCAO_PROBLEMAS.md)
- [`docs/MELHORIAS.md`](docs/MELHORIAS.md)

Configuração MCP partilhada do projeto: [`.cursor/mcp.json`](.cursor/mcp.json) — após editar, reinicie o Cursor.

## WinGet (referência upstream)

Acompanhe versões e mudanças do CLI em [Releases · microsoft/winget-cli](https://github.com/microsoft/winget-cli/releases).
