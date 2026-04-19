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
| `build` | `compilar` | `dotnet build Ribanense.Solucoes.slnx` (encerra processos Ribanense deste repo antes). |
| `run` `[App]` | `rodar` | Compila e abre o Launcher. Se passar um nome de app (`rb run Winget`), abre o app direto. |
| `test` | `testar` | `dotnet test Ribanense.Solucoes.slnx`. |
| `check` | `validar` | `build` + `test` em sequência. |
| `clean` | `limpar` | Remove todos os `bin/`, `obj/` e `artifacts/` do repo. Encerra processos Ribanense primeiro. |
| `list` | `apps`, `ls` | Lista os apps em `src/aplicativos/` com versão do `.csproj` e do `app.json`. |
| `version` | `versao` | Mostra versões do Launcher, do SDK e de cada app. Alerta quando `csproj` e `app.json` divergem. |
| `devlink <App>` | `link` | Compila um app e copia para `%LOCALAPPDATA%\Ribanense Soluções\aplicativos\<App>\` para o Launcher reconhecê-lo como "instalado" sem precisar publicar release. |
| `unlink <App>` | `devunlink` | Remove o devlink de um app. |
| `publish <App> [-Version <ver>]` | `empacotar` | Gera pacote local do app em `artifacts/publish/<App>/` (zip + sha256 + app.json). |
| `release <App> <semver>` | — | Publica GitHub Release via `gh` (cria tag, faz upload do zip/sha/app.json). |
| `logs [App] [N]` | `log` | Imprime as últimas N (default 100) entradas do vault estruturado. Sem args = Launcher. Usa cópia temporária do `.dat` para não conflitar com processo rodando. |
| `crashlog` | `crash` | Mostra as últimas 200 linhas do `crash.log` (texto plano). Inclui `crash.old.log` rotacionado se existir. |
| `crashlog-clear` | `crash-clear` | Remove `crash.log` e `crash.old.log`. |
| `help` | `?`, `-h`, `--help` | Ajuda. |

### Exemplos

```bat
cd caminho\para\RibanenseSolucoes
rb.cmd list
rb.cmd run
rb.cmd run Winget
rb.cmd test
rb.cmd check
rb.cmd devlink Winget
rb.cmd unlink Winget
rb.cmd version
rb.cmd clean
rb.cmd publish Winget -Version 0.1.0
rb.cmd release Winget 0.1.0
```

### Fluxo de desenvolvimento recomendado

1. `rb list` — ver o que está no repo.
2. `rb run Winget` — testar o app isolado.
3. `rb devlink Winget` — empacotar no formato que o Launcher entende.
4. `rb run` — abrir o Launcher e ver o Winget em "Meus apps".
5. `rb check` — antes de commitar.
6. `rb publish Winget -Version 0.2.0` + `rb release Winget 0.2.0` — quando estiver pronto para publicar.

### Dicas

- **Stack trace detalhado**: defina `RIBANENSE_CLI_TRACE=1` para ver o stack trace completo quando um comando falhar.
- **Código de saída**: `0` em sucesso, `1` em comando desconhecido ou erro capturado; códigos do `dotnet` são propagados se a falha vier dele.
- **Processos em execução**: `build`, `run`, `clean` e `devlink` tentam encerrar instâncias do Launcher e dos apps Ribanense deste repo antes de operar, para evitar locks em DLLs.
- **Ajuda auto-gerada**: a saída de `rb help` é construída a partir da tabela de comandos no próprio script; para adicionar um comando novo, basta acrescentar uma entrada em `$script:Commands` e implementar o handler.

## Logs e troubleshooting em runtime

Cada app Ribanense grava logs em duas camadas paralelas (ambas acessíveis via CLI):

| Camada | Onde | Lido por | Conteúdo |
|---|---|---|---|
| Vault estruturado (LiteDB) | `%LOCALAPPDATA%\Ribanense Soluções\Launcher.dat` e `apps\<id>\<Nome>.dat` | `rb logs [App] [N]` | Metadados, settings, logs categorizados (startup, install.done, unhandled, etc.), trilha de auditoria. |
| Crash log (texto plano) | `%LOCALAPPDATA%\Ribanense Soluções\crash.log` | `rb crashlog` | Apenas unhandled exceptions, em texto UTF-8, com rotação ao atingir 1 MB (vira `crash.old.log`). Nunca bloqueia, nunca falha. |

Para investigar um erro visível na UI:

1. `rb crashlog` — mostra exceções não tratadas recentes, incluindo toda a cadeia de `InnerException` e stack trace.
2. `rb logs` — contexto estruturado antes do erro (últimas operações, status, etc.). Passa `App` como primeiro arg para inspecionar um app específico (`rb logs Winget`).
3. `rb crashlog-clear` — reseta para começar uma sessão de teste do zero.

Os próprios `.exe` expõem as mesmas informações via CLI, úteis no Launcher ou em scripts:

```bat
Ribanense.Solucoes.Launcher.exe --version       # {"version":"x.y.z","sdk":"x.y.z"}
Ribanense.Solucoes.Launcher.exe --selfcheck     # valida paths, exit 0/1
Ribanense.Solucoes.Launcher.exe --logs 50       # últimas 50 entradas
Ribanense.Solucoes.App.Winget.exe --logs 200    # idem para o app
```

## CLIs externas recomendadas

| Ferramenta | Função |
|------------|--------|
| `dotnet` | Build, run e test da solution. |
| `git` | Tags e histórico do monorepo. |
| `gh` (GitHub CLI) | Publicação de releases (`rb release` depende disso). [cli.github.com](https://cli.github.com/). |
| `winget` | Runtime de domínio do app **Gestor WinGet**. [Releases · microsoft/winget-cli](https://github.com/microsoft/winget-cli/releases). |
| LiteDB Studio | GUI para inspecionar arquivos `.dat` (Launcher.dat, Winget.dat) gerados em `%LOCALAPPDATA%\Ribanense Soluções\`. |

## Integração com agentes e CI

- **Agentes (Cursor):** preferir `.\rb.cmd compilar`, `.\rb.cmd test` e `.\rb.cmd check` a partir da raiz. Use `rb list` para descobrir apps disponíveis de forma programática.
- **CI:** `dotnet build .\Ribanense.Solucoes.slnx` e `dotnet test`. Para artefatos, invocar `ferramentas/publish-module.ps1 -App <Nome> -Version <semver>`.

## Ver também

- [`../README.md`](../README.md)
- [`AMBIENTE.md`](AMBIENTE.md)
- [`RELEASE_PROCESS.md`](RELEASE_PROCESS.md)
- [`FERRAMENTAS_IA.md`](FERRAMENTAS_IA.md)
