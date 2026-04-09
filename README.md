# BananaSuisa (modular)

## Onde fica cada coisa

| Local | Função |
|-------|--------|
| [`BananaSuisa.ps1`](BananaSuisa.ps1) | Script único gerado para uso no PC (execute como administrador). |
| [`BananaSuisa.bat`](BananaSuisa.bat) | Atalho que inicia `BananaSuisa.ps1`. |
| [`BananaSuisa_desenvolvimento/`](BananaSuisa_desenvolvimento/) | Código-fonte em módulos (`nucleo`, `interface`, `funcionalidades`, `eventos`). |
| [`BananaSuisa_desenvolvimento/nucleo/versao.ps1`](BananaSuisa_desenvolvimento/nucleo/versao.ps1) | **Versão única** (`$script:BananaSuisaVersao`) — editar aqui; o gerador embute no `BananaSuisa.ps1` e a UI mostra no título. |
| [`BananaSuisa_recursos/`](BananaSuisa_recursos/) | Modelos (JSON, config) na raiz desta pasta; dados de execução em `BananaSuisa_recursos\BananaSuisa_memoria\`. |
| [`ferramentas/Gerar_BananaSuisa.ps1`](ferramentas/Gerar_BananaSuisa.ps1) | Consolida os módulos em `BananaSuisa.ps1` (remove o `BananaSuisa.ps1` anterior e o legado `BananaSuisa_PRO.ps1` se existirem). |
| [`bs.cmd`](bs.cmd) / [`ferramentas/BananaSuisa.cmd`](ferramentas/BananaSuisa.cmd) | CLI de desenvolvimento: `build`, `versao`, `help` (ver [`docs/FERRAMENTAS_CLI.md`](docs/FERRAMENTAS_CLI.md)). |
| [`BananaSuisa.slnx`](BananaSuisa.slnx) | Solution inicial da migração para .NET. |
| [`src/`](src/) | Projetos `BananaSuisa.App`, `Core`, `Services`, `Infrastructure` e `Shared`. |
| [`.cursor/mcp.json`](.cursor/mcp.json) | Configuração MCP partilhada do projeto. |

Para empacotar fora da pasta do projeto, copie `BananaSuisa_desenvolvimento`, `BananaSuisa_recursos` e um `main.ps1` que faça dot-source dos módulos (mesma ordem que em `ferramentas/Gerar_BananaSuisa.ps1`).

## Memória (`BananaSuisa_recursos\BananaSuisa_memoria`)

Tudo fica **no projeto**, dentro de uma subpasta de recursos — não usa `%LOCALAPPDATA%\BananaSuisa_memoria` para o estado do app (na primeira execução após atualizar, dados antigos podem ser **movidos** para cá).

- **Registros:** `BananaSuisa.json` (log no painel) fica em `...\BananaSuisa_memoria\Registros\`.
- Outras subpastas: `Dados`, `Perfis`, `ScriptsExtras`, `Temporarios`, `DriversImpressoras`, `PacotesBaixados` (e `PacotesBaixados\WinGet`). Veja `LEIA-ME.txt` dentro de `BananaSuisa_memoria`.

Apagar a pasta `BananaSuisa_memoria` (com o app fechado) redefine o app aos padrões dos modelos em `BananaSuisa_recursos`.

## Gerar o script consolidado

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\ferramentas\Gerar_BananaSuisa.ps1
```

Ou pela CLI:

```bat
.\bs.cmd build
```

Saída: `BananaSuisa.ps1` na raiz (substitui o arquivo anterior).

## Esqueleto .NET

Build inicial da solution:

```powershell
dotnet build .\BananaSuisa.slnx
```

Execução da janela bootstrap WPF:

```powershell
dotnet run --project .\src\BananaSuisa.App\BananaSuisa.App.csproj
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
