# Contribuindo com Ribanense Soluções

Fluxo mínimo para evoluir o Launcher e os apps do catálogo.

## Fluxo rápido

1. Trabalhe em C# e XAML dentro de `src/`.
2. `.\rb.cmd compilar`, `.\rb.cmd test` ou `.\rb.cmd check` durante o desenvolvimento.
3. Para mudanças que afetam runtime do Windows (winget, UWP, drivers, UAC), teste como administrador.
4. Atualize documentação sempre que mudar comportamento, requisitos ou comandos.

## Estrutura do código

| Local | Responsabilidade |
|-------|------------------|
| `src/Ribanense.Solucoes.Launcher/` | UI do Launcher, serviços de catálogo, instalação, atualização. |
| `src/Ribanense.Solucoes.PluginSDK/` | Contratos publicados. **Mudança quebra SemVer**: incrementar major quando quebrar. |
| `src/Ribanense.Solucoes.Infrastructure/` | LiteDB, logging JSON, IO. Sem UI. |
| `src/Ribanense.Solucoes.UI/` | Temas, breakpoints, controles WPF reutilizáveis, base MVVM. |
| `src/aplicativos/Ribanense.Solucoes.App.<Nome>/` | Um app autônomo por pasta. |
| `tests/` | Testes por camada/app. |
| `ferramentas/` | CLI `Ribanense.cli.ps1` e scripts de publicação. |

## Regras práticas

- Nome público com acento: **Ribanense Soluções**. Namespaces/paths/tags: **Ribanense.Solucoes** (ASCII).
- Nenhum `Width`/`Height` em pixels na janela raiz. Use `MinWidth` lógico + breakpoints (Compact <768, Medium <1200, Wide >=1200).
- Cada app **compila e roda sem o Launcher** (fallback em defaults locais quando `RIBANENSE_APP_DATA` não está setado).
- Launcher **nunca depende** de um app em tempo de compilação. Comunicação é via `app.json` + CLI (`--version`, `--selfcheck`) + variáveis de ambiente.
- Preserve separação de camadas: App → SDK/Infrastructure/UI, nunca o inverso.
- Sem segredos commitados (tokens, chaves). Para o `release.ps1`, use `gh auth login` localmente.

## Validação mínima

1. `.\rb.cmd compilar` sem warnings novos.
2. `.\rb.cmd test` verde.
3. `.\rb.cmd run` abre o Launcher e a mudança funciona.
4. Se tocou em instalação/reparo do Windows: testar elevado.
5. Revisar documentos afetados.

## Reportar bugs

Inclua, quando aplicável:

- Versão do Windows.
- Execução elevada (sim/não).
- Comando exato usado (`rb.cmd run`, etc.).
- Código de saída de `winget`, `gh` ou similar quando houver.
- Caminho do log do app (em `%LOCALAPPDATA%/Ribanense Solucoes/apps/<id>/<Nome>.dat`).
- Passos para reproduzir, resultado esperado vs obtido.

## Documentação relacionada

- [`docs/INDICE.md`](docs/INDICE.md)
- [`docs/ARQUITETURA.md`](docs/ARQUITETURA.md)
- [`docs/PLUGIN_SDK.md`](docs/PLUGIN_SDK.md)
- [`docs/RELEASE_PROCESS.md`](docs/RELEASE_PROCESS.md)
- [`docs/AMBIENTE.md`](docs/AMBIENTE.md)
- [`docs/FERRAMENTAS_CLI.md`](docs/FERRAMENTAS_CLI.md)
- [`docs/FERRAMENTAS_IA.md`](docs/FERRAMENTAS_IA.md)
