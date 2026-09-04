# Índice de documentação

Ponto de entrada da documentação do **Ribanense Soluções**. Use como mapa antes de abrir documentos específicos.

## Comece aqui

| Documento | Público principal | Quando ler |
|-----------|-------------------|------------|
| [`../README.md`](../README.md) | Todos | Visão geral do monorepo, build e pastas. |
| [`../CONTRIBUTING.md`](../CONTRIBUTING.md) | Contribuidores | Antes de editar código. |
| [`AMBIENTE.md`](AMBIENTE.md) | Contribuidores | Para configurar máquina e pré-requisitos. |
| [`ARQUITETURA.md`](ARQUITETURA.md) | Contribuidores | Para entender Launcher + apps + catálogo + releases. |

## Arquitetura e contratos

| Documento | Foco |
|-----------|------|
| [`ARQUITETURA.md`](ARQUITETURA.md) | Visão do sistema: Launcher, apps, catálogo, GitHub Releases, fluxo de atualização. |
| [`PLUGIN_SDK.md`](PLUGIN_SDK.md) | Contrato `app.json`, CLI obrigatório dos apps, variáveis de ambiente. |
| [`RELEASE_PROCESS.md`](RELEASE_PROCESS.md) | Tag, publish, SHA256, `gh release create`. |

## Operação

| Documento | Foco |
|-----------|------|
| [`FERRAMENTAS_CLI.md`](FERRAMENTAS_CLI.md) | `rb.cmd` e ferramentas externas usadas. |
| [`APP_CHOCOLATEY.md`](APP_CHOCOLATEY.md) | App Gestor Chocolatey, comandos `choco` usados, parsing e riscos. |
| [`APP_BALANCA.md`](APP_BALANCA.md) | App Testador de Balanças: serial COM/USB, protocolos, timing, varredura e troubleshooting. |
| [`APP_FAROL.md`](APP_FAROL.md) | App Farol: coletores de evidência, regras, malha LAN (UDP/TCP), pareamento, bandeja e autostart. |
| [`FERRAMENTAS_IA.md`](FERRAMENTAS_IA.md) | MCP/IA aplicados ao desenvolvimento deste repositório. |
| [`REFERENCIAS_EXTERNAS.md`](REFERENCIAS_EXTERNAS.md) | Links para specs, bibliotecas e documentação canônica. |

## Hardware

| Documento | Foco |
|-----------|------|
| [`../hardware/README.md`](../hardware/README.md) | Inventário de equipamentos físicos documentados. |
| [`../hardware/esp32-2432s028r/README.md`](../hardware/esp32-2432s028r/README.md) | Display ESP32-32E 2,8" (SKU `E32R28T-1`, família CYD): fotos, identificação e pinout. |
| [`FIRMWARE_RIBANENSEESP.md`](FIRMWARE_RIBANENSEESP.md) | Casca RibanenseESP: UI, SD, OTA, limites da E32R28T-1. |

## Agentes e decisões

| Documento | Foco |
|-----------|------|
| [`../AGENTS.md`](../AGENTS.md) | Regras e contexto para agentes de IA. |

## Convenções

- Preferir nomes em letras MAIÚSCULAS para documentos de processo e referência.
- Atualizar documentação junto com a mudança de comportamento/requisitos.
- Nome público com acento: **Ribanense Soluções**; em código/paths: **Ribanense.Solucoes**.
