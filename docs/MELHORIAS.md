# Melhorias recentes (changelog resumido)

## UI WPF: scroll com a roda do rato (DataGrid, TextBox, ScrollViewer)

**Problema:** Em vistas com `ScrollViewer` (ex.: fluxo Instalar, Logs), grelhas `DataGrid` e caixas de texto com scroll interno capturavam a roda mesmo quando não havia nada a rolar dentro do controlo, ou quando o utilizador pretendia mover a página inteira. `ScrollViewer` aninhados na mesma direção para o mesmo conteúdo também impediam a roda de atuar no scroll “certo”.

**Solução:** Um único scroll vertical na casca do fluxo (ex.: `InstallShellView`); vistas filhas sem segundo `ScrollViewer` redundante onde não for necessário. Comportamentos anexados em `src/BananaSuisa.App/Behaviors/` (`DataGridWheelBehavior` aplicado em `LogsDataGridStyle`; `TextBoxWheelBehavior` no log de instalação em `MainWindow`) encaminham a roda para o `ScrollViewer` da página quando o scroll interno não a absorve. Ver `Behaviors/README.md` na pasta do código.

## Correção de raiz do projeto (`projectRoot`)

**Problema:** Com o script consolidado na raiz do repositório, uma resolução incorreta de `$projectRoot` (`Split-Path -Parent $PSScriptRoot`) apontava para a pasta *pai* do projeto (ex.: Desktop), não para a pasta do BananaSuisa. O `PayloadRoot` deixava de encontrar `BananaSuisa_recursos` (antes `payload`), e catálogo/config podiam não sincronizar — sintoma típico: interface ok, instalação falha ou dados inconsistentes.

**Solução:** Função `Get-BananaSuisaProjectRoot` que testa candidatos (`$PSScriptRoot`, pais) até localizar uma pasta que contenha `BananaSuisa_recursos` ou `payload` (legado).

## Pastas e nomes em português

- Estado do usuário: `BananaSuisa_recursos\BananaSuisa_memoria` (antes `BananaSuisa.Data` / `%LOCALAPPDATA%\BananaSuisa_memoria`).
- Marcador portátil: `BananaSuisa_modo_portatil` (antes `state.portable`), com migração do arquivo antigo.
- Recursos embutidos: `BananaSuisa_recursos` (antes `payload`).
- Código modular: `BananaSuisa_desenvolvimento/` com subpastas `nucleo`, `interface`, `funcionalidades`, `eventos`.
- Build: `ferramentas/Gerar_BananaSuisa.ps1` (gera `BananaSuisa.ps1`; antes `gerar-pro.ps1` / nome PRO).

## Instalação / WinGet

- `Get-BananaSuisaWingetExe`: resolve caminho do `winget.exe` (PATH, WindowsApps, etc.).
- Leitura de **stderr** nos laços de instalação/atualização/remoção para registrar avisos no log.

## Breaking changes

Quem ainda tinha apenas `BananaSuisa.Data` ou `state.portable` verá migração automática na primeira execução. Caminhos fixos em scripts externos que apontavam para nomes antigos precisam ser atualizados.
