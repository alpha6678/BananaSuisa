# Ribanense Soluções

Launcher e catálogo de aplicativos modulares para Windows, inspirado no modelo Adobe Creative Cloud. Cada app é um `.exe` independente, baixado sob demanda via GitHub Releases. Atualizações são granulares por app, sem servidor próprio.

## Mapa do repositório

| Local | Função |
|-------|--------|
| [`Ribanense.Solucoes.slnx`](Ribanense.Solucoes.slnx) | Solution .NET do monorepo. |
| [`src/Ribanense.Solucoes.Launcher/`](src/Ribanense.Solucoes.Launcher/) | App WPF do launcher (catálogo, instalador, atualizador). |
| [`src/Ribanense.Solucoes.PluginSDK/`](src/Ribanense.Solucoes.PluginSDK/) | Contratos versionados entre launcher e apps. |
| [`src/Ribanense.Solucoes.Infrastructure/`](src/Ribanense.Solucoes.Infrastructure/) | Implementações de infraestrutura compartilhada (LiteDB, log). |
| [`src/Ribanense.Solucoes.UI/`](src/Ribanense.Solucoes.UI/) | Estilos, breakpoints responsivos, base MVVM. |
| `src/aplicativos/` | Cada app do catálogo em subpasta própria (adicionados nas próximas fases). |
| [`ferramentas/`](ferramentas/) | CLI de desenvolvimento e scripts de release. |
| [`docs/`](docs/) | Documentação de arquitetura, SDK e processo de release. |

## Build e execução

```bat
.\rb.cmd compilar
.\rb.cmd run
.\rb.cmd test
.\rb.cmd check
```

Equivalentes diretos com `dotnet`:

```powershell
dotnet build .\Ribanense.Solucoes.slnx
dotnet run --project .\src\Ribanense.Solucoes.Launcher\Ribanense.Solucoes.Launcher.csproj
dotnet test  .\Ribanense.Solucoes.slnx
```

## Publicação de um app do catálogo

```bat
.\rb.cmd publish Winget -Version 1.0.0
.\rb.cmd release Winget 1.0.0
```

O primeiro comando gera `artifacts/publish/Winget/winget-1.0.0-win-x64.zip` + `.sha256` + cópia do `app.json`. O segundo cria a tag `winget-v1.0.0` e publica o GitHub Release com os assets.

Detalhes: [`docs/RELEASE_PROCESS.md`](docs/RELEASE_PROCESS.md).

## Documentação central

- [`docs/INDICE.md`](docs/INDICE.md) — índice completo da documentação.
- [`docs/ARQUITETURA.md`](docs/ARQUITETURA.md) — como Launcher, apps, catálogo e GitHub Releases se encaixam.
- [`docs/PLUGIN_SDK.md`](docs/PLUGIN_SDK.md) — contrato `app.json`, CLI dos apps e variáveis de ambiente.
- [`docs/RELEASE_PROCESS.md`](docs/RELEASE_PROCESS.md) — processo de tag, build, assinatura e publicação.
- [`CONTRIBUTING.md`](CONTRIBUTING.md) — fluxo de contribuição.
- [`AGENTS.md`](AGENTS.md) — regras para agentes de IA no repositório.

## WinGet (referência upstream)

Acompanhe versões e mudanças do CLI em [Releases · microsoft/winget-cli](https://github.com/microsoft/winget-cli/releases).
