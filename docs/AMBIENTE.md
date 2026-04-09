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
.\bs.cmd compilar
.\bs.cmd run
.\bs.cmd test
.\bs.cmd check
```

Equivalentes diretos no .NET:

```powershell
dotnet build .\BananaSuisa.slnx
dotnet run --project .\src\BananaSuisa.App\BananaSuisa.App.csproj
dotnet test .\BananaSuisa.slnx
```

Observacao: nao e necessario alterar a execution policy global do Windows para usar os comandos acima.

## Pastas e arquivos importantes

| Caminho | Papel |
|---------|-------|
| `BananaSuisa_recursos/` | Modelos, config e arquivos de apoio. |
| `BananaSuisa_recursos/BananaSuisa_memoria/` | Estado, dados em uso, registros e caches locais. |
| `ferramentas/` | CLI de desenvolvimento. |
| `.cursor/mcp.json` | Configuracao MCP compartilhada do workspace. |
| `BananaSuisa.slnx` | Solution da nova base .NET. |
| `src/` | Projetos da aplicacao WPF e camadas de suporte. |

## Setup inicial recomendado

1. Abrir a pasta `BananaSuisa` no Cursor ou VS Code.
2. Confirmar que `dotnet` SDK 10.x está instalado.
3. Executar `.\bs.cmd compilar`.
4. Executar `.\bs.cmd test` para validar os testes automatizados da solution.
5. Se for usar MCP Playwright, confirmar `node` e `npx` no `PATH`.
6. Reiniciar o Cursor se alterar `.cursor/mcp.json`.
7. Testar a interface com `.\bs.cmd run` (ou Visual Studio elevado).

## Requisitos reservados para as proximas fases

Os itens abaixo ainda nao sao obrigatorios para todos os fluxos do projeto, mas entram nas proximas fases da migracao:

- Ferramenta de empacotamento MSI, com preferencia atual por WiX.
- Estrategia de testes desktop para a UI futura (por exemplo, WinAppDriver ou Appium, conforme a stack escolhida).

## Problemas comuns de setup

| Sintoma | Acao sugerida |
|---------|---------------|
| `.\bs.cmd` nao executa | Confirmar se esta na pasta `BananaSuisa` e se `powershell` ou `pwsh` esta disponivel. |
| `.\bs.cmd compilar` ou `.\bs.cmd test` falha | Confirmar se o `dotnet` SDK 10.x esta instalado e disponivel no `PATH`. |
| `winget` nao e encontrado | Verificar App Installer, Store e rotinas de reparo documentadas em `actions.ps1` e `SOLUCAO_PROBLEMAS.md`. |
| `npx` nao e encontrado | Instalar Node.js LTS e reabrir o terminal. |
| O app nao grava dados | Confirmar permissao de escrita em `BananaSuisa_recursos\BananaSuisa_memoria`. |

## Ver tambem

- [`../CONTRIBUTING.md`](../CONTRIBUTING.md)
- [`FERRAMENTAS_CLI.md`](FERRAMENTAS_CLI.md)
- [`FERRAMENTAS_IA.md`](FERRAMENTAS_IA.md)
- [`../BananaSuisa_desenvolvimento/docs/SOLUCAO_PROBLEMAS.md`](../BananaSuisa_desenvolvimento/docs/SOLUCAO_PROBLEMAS.md)
