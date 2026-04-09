# Ambiente de desenvolvimento

Este documento concentra os requisitos atuais para trabalhar no BananaSuisa e os requisitos previstos para a futura migracao para .NET.

## Requisitos obrigatorios hoje

| Categoria | Requisito | Notas |
|-----------|-----------|-------|
| Sistema operacional | Windows 10 ou Windows 11 | O produto e focado em desktop Windows. |
| Shell principal | Windows PowerShell 5.1 | O codigo atual usa `#Requires -Version 5.1`. |
| Permissao | Execucao como administrador para fluxos sensiveis | Necessario para varios cenarios de `winget`, reparo, drivers e scripts de sistema. |
| Runtime do app | `winget` / App Installer disponivel ou reparavel | O produto depende do ecossistema `Microsoft.DesktopAppInstaller`. |
| Rede | Acesso a internet para fluxos online | Necessario para metadata, downloads e fontes do `winget`. |
| Workspace | Pasta da aplicacao com permissao de escrita | O app grava estado em `BananaSuisa_recursos\BananaSuisa_memoria`. |

## Ferramentas recomendadas

| Ferramenta | Obrigatoria | Uso |
|------------|-------------|-----|
| Cursor ou VS Code | Nao | Edicao do projeto. |
| Extensao PowerShell | Nao | Melhor navegacao, syntax highlight e execucao de scripts. |
| `git` | Nao | Historico e colaboracao. |
| `pwsh` (PowerShell 7) | Nao | Shell moderno; o wrapper `BananaSuisa.cmd` usa `pwsh` se estiver disponivel. |
| Node.js 18+ e `npx` | Nao | Necessarios para o Playwright MCP em `.cursor/mcp.json`. |
| .NET SDK 10.0.x | Nao | Necessario para compilar e evoluir a solution `BananaSuisa.slnx`. |
| Pester | Nao | Base para testes PowerShell futuros. |

## Comandos basicos

```bat
.\bs.cmd help
.\bs.cmd versao
.\bs.cmd build
```

Alternativa direta:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\ferramentas\Gerar_BananaSuisa.ps1
```

```powershell
dotnet build .\BananaSuisa.slnx
dotnet run --project .\src\BananaSuisa.App\BananaSuisa.App.csproj
```

Observacao: nao e necessario alterar a execution policy global do Windows para usar os comandos acima.

## Pastas e arquivos importantes

| Caminho | Papel |
|---------|-------|
| `BananaSuisa_desenvolvimento/` | Fonte modular do projeto. |
| `BananaSuisa.ps1` | Script consolidado gerado para execucao. |
| `BananaSuisa_recursos/` | Modelos, config e arquivos de apoio. |
| `BananaSuisa_recursos/BananaSuisa_memoria/` | Estado, dados em uso, registros e caches locais. |
| `ferramentas/` | Build e CLI de desenvolvimento. |
| `.cursor/mcp.json` | Configuracao MCP compartilhada do workspace. |
| `BananaSuisa.slnx` | Solution da nova base .NET. |
| `src/` | Projetos da aplicacao WPF e camadas de suporte. |

## Setup inicial recomendado

1. Abrir a pasta `BananaSuisa` no Cursor ou VS Code.
2. Confirmar que `powershell` funciona no terminal.
3. Executar `.\bs.cmd build`.
4. Executar `dotnet build .\BananaSuisa.slnx` se for trabalhar na base .NET.
5. Se for usar MCP Playwright, confirmar `node` e `npx` no `PATH`.
6. Reiniciar o Cursor se alterar `.cursor/mcp.json`.
7. Testar `BananaSuisa.ps1` como administrador antes de mexer em fluxos de instalacao.

## Requisitos reservados para as proximas fases

Os itens abaixo ainda nao sao obrigatorios para todos os fluxos do projeto, mas entram nas proximas fases da migracao:

- Ferramenta de empacotamento MSI, com preferencia atual por WiX.
- Estrategia de testes desktop para a UI futura (por exemplo, WinAppDriver ou Appium, conforme a stack escolhida).

## Problemas comuns de setup

| Sintoma | Acao sugerida |
|---------|---------------|
| `.\bs.cmd` nao executa | Confirmar se esta na pasta `BananaSuisa` e se `powershell` ou `pwsh` esta disponivel. |
| `winget` nao e encontrado | Verificar App Installer, Store e rotinas de reparo documentadas em `actions.ps1` e `SOLUCAO_PROBLEMAS.md`. |
| `npx` nao e encontrado | Instalar Node.js LTS e reabrir o terminal. |
| O app nao grava dados | Confirmar permissao de escrita em `BananaSuisa_recursos\BananaSuisa_memoria`. |

## Ver tambem

- [`../CONTRIBUTING.md`](../CONTRIBUTING.md)
- [`FERRAMENTAS_CLI.md`](FERRAMENTAS_CLI.md)
- [`FERRAMENTAS_IA.md`](FERRAMENTAS_IA.md)
- [`../BananaSuisa_desenvolvimento/docs/SOLUCAO_PROBLEMAS.md`](../BananaSuisa_desenvolvimento/docs/SOLUCAO_PROBLEMAS.md)
