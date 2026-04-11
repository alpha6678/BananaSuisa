# Ferramentas de linha de comandos (CLI)

Guia das interfaces de terminal do repositório e das CLIs externas úteis ao desenvolvimento do BananaSuisa.

## CLI do projeto (Windows)

| Entrada | Descrição |
|---------|-----------|
| [`bs.cmd`](../bs.cmd) | Atalho na raiz da pasta da aplicação: delega para `ferramentas\BananaSuisa.cmd`. |
| [`ferramentas/BananaSuisa.cmd`](../ferramentas/BananaSuisa.cmd) | Usa **PowerShell 7** (`pwsh`) se existir no `PATH`; caso contrário, **Windows PowerShell 5.1**. |
| [`ferramentas/BananaSuisa.cli.ps1`](../ferramentas/BananaSuisa.cli.ps1) | Script PowerShell com os subcomandos (pode invocar-se diretamente). |

### Comandos

| Comando | Sinónimos | Ação |
|---------|-----------|------|
| `compilar` | `build`, `build-dotnet`, `dotnet-build` | Executa `dotnet build .\BananaSuisa.slnx`. |
| `run` | `rodar`, `ui` | Compila o projeto da app e inicia o `.exe` de Debug com UAC (ver script em `ferramentas/BananaSuisa.cli.ps1`). |
| `test` | `testar` | Executa `dotnet test .\BananaSuisa.slnx`. |
| `check` | `validar` | Executa `compilar` + `test` em sequencia. |
| `publish` | `empacotar`, `package` | Publica a app WPF em **Release**, **win-x64**, **self-contained**, **ficheiro único** (`PublishSingleFile`, compressão, nativos embutidos), com ReadyToRun, para `artifacts\publish\BananaSuisa.App.exe`. A pasta `artifacts\` está no `.gitignore`. |
| `help` | `?`, `-h`, `--help` | Mostra a ajuda no terminal. |

### Exemplos

```bat
cd caminho\para\BananaSuisa
bs.cmd compilar
bs.cmd run
bs.cmd test
bs.cmd check
bs.cmd publish
```

```powershell
Set-Location caminho\para\BananaSuisa
.\ferramentas\BananaSuisa.cli.ps1 build
.\ferramentas\BananaSuisa.cli.ps1 compilar
```

Código de saída: `0` em sucesso; `1` se o comando for desconhecido (após mostrar ajuda).

---

## CLIs externas recomendadas

Ferramentas que não fazem parte do repositório mas alinham com o fluxo de trabalho e com a documentação de [FERRAMENTAS_IA.md](FERRAMENTAS_IA.md).

| Ferramenta | Função no contexto BananaSuisa |
|------------|--------------------------------|
| **winget** | Runtime do produto; acompanhar [releases do winget-cli](https://github.com/microsoft/winget-cli/releases). |
| **git** | Controlo de versão do código e dos recursos. |
| **pwsh** (PowerShell 7) | Shell moderno; preferido pelo `BananaSuisa.cmd` quando instalado. [Instalação](https://learn.microsoft.com/powershell/scripting/install/installing-powershell-on-windows). |
| **Node.js + npx** | Necessários para o servidor MCP Playwright em [`.cursor/mcp.json`](../.cursor/mcp.json). |
| **dotnet** | Build, execucao e testes da solution `BananaSuisa.slnx`, incluindo `dotnet build`, `dotnet run` e `dotnet test`. |

### Opcional: testes PowerShell com Pester

Para testes automatizados de funções puras nos módulos (sem abrir a UI):

```powershell
Install-Module Pester -Scope CurrentUser -Force -SkipPublisherCheck
Invoke-Pester
```

(O repositório ainda pode não incluir ficheiros `*.Tests.ps1`; este bloco documenta o padrão quando forem adicionados.)

---

## Integração com agentes e CI

- **Agentes (Cursor):** preferir `.\bs.cmd compilar`, `.\bs.cmd test` e `.\bs.cmd check` a partir da raiz do repositório.
- **Agentes (Cursor) na base .NET:** usar `.\bs.cmd run` para abrir a UI WPF e `.\bs.cmd check` para validar o fluxo principal.
- **Empacotamento local:** `.\bs.cmd publish` gera o `.exe` autonomo (single-file) em `artifacts\publish\BananaSuisa.App.exe` (pode passar argumentos extra do `dotnet publish` após o comando).
- **CI (GitHub Actions, Azure Pipelines, etc.):** usar `dotnet build .\BananaSuisa.slnx` para a nova base; para artefactos de release, reutilizar os mesmos parametros que `publish` ou equivalente `dotnet publish` com `-p:PublishSingleFile=true` e `-p:IncludeNativeLibrariesForSelfExtract=true`.

---

## Ver também

- [README.md](../README.md) — mapa de pastas e geração do consolidado
- [FERRAMENTAS_IA.md](FERRAMENTAS_IA.md) — MCPs e testes complementares
