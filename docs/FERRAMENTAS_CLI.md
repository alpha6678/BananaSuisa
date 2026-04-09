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
| `build` | `gerar` | Executa [`Gerar_BananaSuisa.ps1`](../ferramentas/Gerar_BananaSuisa.ps1); gera `BananaSuisa.ps1` na raiz da pasta da aplicação. |
| `versao` | — | Imprime o valor de `$script:BananaSuisaVersao` definido em [`nucleo/versao.ps1`](../BananaSuisa_desenvolvimento/nucleo/versao.ps1). |
| `help` | `?`, `-h`, `--help` | Mostra a ajuda no terminal. |

### Exemplos

```bat
cd caminho\para\BananaSuisa
bs.cmd build
bs.cmd versao
```

```powershell
Set-Location caminho\para\BananaSuisa
.\ferramentas\BananaSuisa.cli.ps1 build
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
| **dotnet** | Build e evolucao da solution `BananaSuisa.slnx`, incluindo `dotnet build` e `dotnet run`. |

### Opcional: testes PowerShell com Pester

Para testes automatizados de funções puras nos módulos (sem abrir a UI):

```powershell
Install-Module Pester -Scope CurrentUser -Force -SkipPublisherCheck
Invoke-Pester
```

(O repositório ainda pode não incluir ficheiros `*.Tests.ps1`; este bloco documenta o padrão quando forem adicionados.)

---

## Integração com agentes e CI

- **Agentes (Cursor):** preferir `.\bs.cmd build` ou `.\ferramentas\BananaSuisa.cli.ps1 build` a partir da pasta [`BananaSuisa`](../) (onde está `BananaSuisa.ps1` gerado).
- **Agentes (Cursor) na base .NET:** usar `dotnet build .\BananaSuisa.slnx` e, quando necessário, `dotnet run --project .\src\BananaSuisa.App\BananaSuisa.App.csproj`.
- **CI (GitHub Actions, Azure Pipelines, etc.):** usar `powershell` ou `pwsh` com `-NoProfile -ExecutionPolicy Bypass -File .\ferramentas\BananaSuisa.cli.ps1 build` para o legado e `dotnet build .\BananaSuisa.slnx` para a nova base.

---

## Ver também

- [README.md](../README.md) — mapa de pastas e geração do consolidado
- [FERRAMENTAS_IA.md](FERRAMENTAS_IA.md) — MCPs e testes complementares
