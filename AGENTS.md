# AGENTS

Este arquivo orienta agentes de IA que trabalhem no repositório **Ribanense Soluções**.

## Objetivo do produto

Ribanense Soluções é um **launcher** estilo Adobe Creative Cloud para Windows (C# WPF, .NET 10). Ele exibe um catálogo de **aplicativos modulares**, cada um distribuído como `.exe` independente via **GitHub Releases**. O usuário final baixa só o launcher; cada app é instalado sob demanda. Atualizações são granulares por app.

## Mapa rápido do código

| Caminho | Papel |
|---------|-------|
| `Ribanense.Solucoes.slnx` | Solution .NET do monorepo. |
| `src/Ribanense.Solucoes.Launcher/` | App WPF do launcher (catálogo, instalador, atualizador). |
| `src/Ribanense.Solucoes.PluginSDK/` | Contratos versionados (SemVer) entre Launcher e apps: `AppManifest`, `IVault`, `IAppJsonLog`, `SdkVersion`. |
| `src/Ribanense.Solucoes.Infrastructure/` | Implementações de infraestrutura compartilhada (LiteDB, log JSON, IO). |
| `src/Ribanense.Solucoes.UI/` | Estilos, breakpoints responsivos, base MVVM e controles comuns. |
| `src/aplicativos/Ribanense.Solucoes.App.<Nome>/` | Cada app do catálogo vive aqui como `.exe` independente. |
| `tests/` | Projetos de teste por camada e por app. |
| `ferramentas/` | CLI do monorepo (`Ribanense.cli.ps1`, `publish-module.ps1`, `release.ps1`). |
| `catalog/catalog.json` | Catálogo público consumido pelo Launcher via `raw.githubusercontent.com`. |
| `docs/` | Arquitetura, processo de release, contrato do SDK, etc. |

## Regras de naming

- **Nome público** (títulos de janela, README, manifestos, instalador): **Ribanense Soluções** (com ç e õ).
- **Namespaces, pastas, IDs, tags, ASCII-only**: `Ribanense.Solucoes`.
- **IDs de app**: `com.ribanense.<slug>` (ex.: `com.ribanense.winget`).
- **Prefixo de tag de release**: `<slug>-v<semver>` (ex.: `winget-v1.0.0`, `launcher-v1.0.0`).

## Como trabalhar neste repositório

- Responder em pt-BR.
- Mudanças pequenas e localizadas sempre que possível.
- Nenhum app pode depender de outro em tempo de compilação. Comunicação entre launcher e app é via manifesto `app.json` + CLI (`--version`, `--selfcheck`) + variáveis de ambiente (`RIBANENSE_APP_DATA`, `RIBANENSE_APP_HOME`).
- Nenhuma janela WPF pode ter `Width`/`Height` fixos em pixels. Usar `MinWidth` lógico e breakpoints `VisualStateManager` (Compact <768, Medium <1200, Wide >=1200).
- Manter a pasta `IA/` no `.gitignore`: ela é insumo local, nunca dependência de build.

## Comandos úteis

```bat
.\rb.cmd help
.\rb.cmd install
.\rb.cmd compilar
.\rb.cmd run
.\rb.cmd app run winget
.\rb.cmd publish-run Winget
.\rb.cmd test
.\rb.cmd check
.\rb.cmd publish Winget -Version 1.0.0
.\rb.cmd release Winget 1.0.0
.\rb.cmd publish all --dry-run
```

## Quando usar subagentes

- Agente de exploração para mapear áreas amplas ou localizar tipos/serviços.
- Agente de shell para build/git/gh multi-etapa.
- Agente geral para refatorações em múltiplos arquivos.

## Validação esperada

- Documentação: revisar links e coerência com os arquivos reais.
- Código .NET: `.\rb.cmd compilar`, `.\rb.cmd test` ou `.\rb.cmd check`.
- Mudanças de runtime (winget, UWP, drivers): indicar claramente que resta validação manual no Windows, idealmente com privilégios elevados.

## Documentação de apoio

- [`README.md`](README.md)
- [`CONTRIBUTING.md`](CONTRIBUTING.md)
- [`docs/INDICE.md`](docs/INDICE.md)
- [`docs/ARQUITETURA.md`](docs/ARQUITETURA.md)
- [`docs/PLUGIN_SDK.md`](docs/PLUGIN_SDK.md)
- [`docs/RELEASE_PROCESS.md`](docs/RELEASE_PROCESS.md)
- [`docs/AMBIENTE.md`](docs/AMBIENTE.md)
- [`docs/FERRAMENTAS_CLI.md`](docs/FERRAMENTAS_CLI.md)
- [`docs/FERRAMENTAS_IA.md`](docs/FERRAMENTAS_IA.md)
