# Ferramentas de linha de comandos

Guia das interfaces de terminal do repositório e das CLIs externas relevantes.

## CLI do projeto

| Entrada | Descrição |
|---------|-----------|
| [`rb.cmd`](../rb.cmd) | Atalho na raiz; delega para `ferramentas/Ribanense.cmd`. |
| [`ferramentas/Ribanense.cmd`](../ferramentas/Ribanense.cmd) | Usa `pwsh` se existir no PATH; senão `powershell` 5.1. |
| [`ferramentas/Ribanense.cli.ps1`](../ferramentas/Ribanense.cli.ps1) | Script PowerShell com os subcomandos; pode ser invocado diretamente. |
| [`ferramentas/publish-module.ps1`](../ferramentas/publish-module.ps1) | Empacota um app em zip + SHA256 + `app.json`. |
| [`ferramentas/release.ps1`](../ferramentas/release.ps1) | Publica release no GitHub via `gh`. |

### Comandos

| Comando | Sinônimos | Ação |
|---------|-----------|------|
| `compilar` | `build` | `dotnet build Ribanense.Solucoes.slnx`. |
| `run` | `rodar` | Compila e abre o Launcher. |
| `test` | `testar` | `dotnet test Ribanense.Solucoes.slnx`. |
| `check` | `validar` | `compilar` + `test`. |
| `publish <App> [-Version <semver>]` | `empacotar` | Gera pacote local do app em `artifacts/publish/<App>/`. |
| `release <App> <semver>` | — | Cria tag e publica GitHub Release com os assets. |
| `help` | `?`, `-h`, `--help` | Ajuda. |

### Exemplos

```bat
cd caminho\para\RibanenseSolucoes
rb.cmd compilar
rb.cmd run
rb.cmd test
rb.cmd check
rb.cmd publish Winget -Version 1.0.0
rb.cmd release Winget 1.0.0
```

Código de saída: `0` em sucesso; `1` se o comando for desconhecido.

## CLIs externas recomendadas

| Ferramenta | Função |
|------------|--------|
| `dotnet` | Build, run e test da solution. |
| `git` | Tags e histórico do monorepo. |
| `gh` (GitHub CLI) | Publicação de releases. [cli.github.com](https://cli.github.com/). |
| `winget` | Runtime de domínio do app Winget (quando existir). [Releases · microsoft/winget-cli](https://github.com/microsoft/winget-cli/releases). |

## Integração com agentes e CI

- **Agentes (Cursor):** preferir `.\rb.cmd compilar`, `.\rb.cmd test` e `.\rb.cmd check` a partir da raiz.
- **CI:** `dotnet build .\Ribanense.Solucoes.slnx` e `dotnet test`. Para artefatos, invocar `ferramentas/publish-module.ps1 -App <Nome> -Version <semver>`.

## Ver também

- [`../README.md`](../README.md)
- [`AMBIENTE.md`](AMBIENTE.md)
- [`RELEASE_PROCESS.md`](RELEASE_PROCESS.md)
- [`FERRAMENTAS_IA.md`](FERRAMENTAS_IA.md)
