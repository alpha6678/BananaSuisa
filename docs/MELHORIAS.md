# Melhorias recentes (changelog resumido)

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
