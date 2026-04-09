# Referencias externas

Esta pagina centraliza links oficiais e fontes confiaveis usadas pelo BananaSuisa hoje e na futura migracao para .NET.

## Runtime e ecossistema WinGet

| Recurso | Link | Uso no projeto |
|---------|------|----------------|
| WinGet CLI | [learn.microsoft.com/windows/package-manager/winget](https://learn.microsoft.com/en-us/windows/package-manager/winget/) | Comportamento do CLI, comandos, sintaxe e operacao base do produto. |
| Repositorio `winget-cli` | [github.com/microsoft/winget-cli](https://github.com/microsoft/winget-cli) | Releases, codigo-fonte, mudancas de comportamento e modulo PowerShell ligado ao ecossistema WinGet. |
| Repositorio `winget-pkgs` | [github.com/microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) | Referencia do catalogo comunitario e manifests publicados. |
| Microsoft.WinGet.Client | [powershellgallery.com/packages/Microsoft.WinGet.Client](https://www.powershellgallery.com/packages/Microsoft.WinGet.Client) | Referencia do modulo PowerShell usado em partes do fluxo e importante para a ponte futura. |

Observacao: antes de apoiar novos fluxos neste modulo, confirme a compatibilidade da versao escolhida com a runtime PowerShell usada pelo projeto.

## PowerShell e testes

| Recurso | Link | Uso no projeto |
|---------|------|----------------|
| PowerShell 5.1 | [learn.microsoft.com/powershell/scripting/overview](https://learn.microsoft.com/en-us/powershell/scripting/overview) | Base da runtime atual do BananaSuisa. |
| Pester | [pester.dev](https://pester.dev/) | Base recomendada para testes automatizados PowerShell. |
| Installacao do Pester | [pester.dev/docs/introduction/installation](https://pester.dev/docs/introduction/installation) | Referencia de setup quando os testes entrarem no repositorio. |
| PSWindowsUpdate | [powershellgallery.com/packages/PSWindowsUpdate](https://www.powershellgallery.com/packages/PSWindowsUpdate/) | Referencia do modulo usado para cenarios de update do Windows. |

## Windows, AppX e empacotamento

| Recurso | Link | Uso no projeto |
|---------|------|----------------|
| Add-AppxPackage | [learn.microsoft.com/powershell/module/appx/add-appxpackage](https://learn.microsoft.com/en-us/powershell/module/appx/add-appxpackage) | Fluxos de reparo e reinstalacao ligados ao App Installer. |
| Windows App SDK | [learn.microsoft.com/windows/apps/windows-app-sdk](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/) | Referencia para stack desktop moderna no Windows. |
| WinUI 3 | [learn.microsoft.com/windows/apps/winui](https://learn.microsoft.com/en-us/windows/apps/winui/) | Opcao de UI para a migracao futura. |
| Desktop Guide (.NET) | [learn.microsoft.com/dotnet/desktop](https://learn.microsoft.com/en-us/dotnet/desktop/) | Documentacao para WPF e Windows Forms em .NET. |
| WPF Overview | [learn.microsoft.com/dotnet/desktop/wpf/overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/) | Referencia caso a migracao opte por WPF. |
| WiX Toolset | [wixtoolset.org/docs/intro](https://wixtoolset.org/docs/intro/) | Empacotamento MSI previsto para a fase de distribuicao. |

## MCPs, automacao e agentes

| Recurso | Link | Uso no projeto |
|---------|------|----------------|
| Model Context Protocol | [modelcontextprotocol.io](https://modelcontextprotocol.io/) | Base conceitual para os MCPs usados no Cursor. |
| Playwright MCP | [npmjs.com/package/@playwright/mcp](https://www.npmjs.com/package/@playwright/mcp) | Servidor MCP versionado no `.cursor/mcp.json`. |
| Playwright MCP docs | [playwright.dev/docs/test-agents#mcp-server](https://playwright.dev/docs/test-agents#mcp-server) | Referencia de uso e setup do servidor Playwright MCP. |

## Como usar esta pagina

- Prefira estes links em novas docs internas em vez de repetir URLs soltas.
- Quando um especialista citar documentacao externa, use esta lista como ponto de partida.
- Se uma dependencia externa mudar de papel no projeto, atualize esta pagina e a documentacao relacionada.

## Relacao com a documentacao interna

- [`INDICE.md`](INDICE.md)
- [`FERRAMENTAS_CLI.md`](FERRAMENTAS_CLI.md)
- [`FERRAMENTAS_IA.md`](FERRAMENTAS_IA.md)
- [`../AGENTS.md`](../AGENTS.md)
- [`../BananaSuisa_desenvolvimento/especialistas/winget_exit_codes.md`](../BananaSuisa_desenvolvimento/especialistas/winget_exit_codes.md)
