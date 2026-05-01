# Gestor Chocolatey - app v0.1

Projeto: [`src/aplicativos/Ribanense.Solucoes.App.Chocolatey/`](../src/aplicativos/Ribanense.Solucoes.App.Chocolatey/).

App independente do catálogo Ribanense Soluções que oferece uma GUI sobre
`choco.exe`, seguindo o mesmo contrato modular do Gestor WinGet sem criar
dependência entre os apps.

## Tabs

| Tab | O que faz |
|-----|-----------|
| **Buscar** | Caixa de texto -> `choco search <query> --limit-output`. O parser usa linhas `nome|versao`. Botão "Instalar" por linha. |
| **Instalados** | `choco list --local-only --limit-output` cruzado com `choco outdated --limit-output` para marcar versões disponíveis. |
| **Fontes** | `choco source list --limit-output`, com remoção de fonte por `choco source remove --name <nome>`. |
| **Módulo** | Diagnóstico simples: localiza `choco.exe`, executa `choco --version` e mostra alerta sobre operações que podem exigir administrador. |

## CLI do app

Todo app Ribanense expõe:

```bat
Ribanense.Solucoes.App.Chocolatey.exe --version
Ribanense.Solucoes.App.Chocolatey.exe --selfcheck
Ribanense.Solucoes.App.Chocolatey.exe --logs 100
```

`--selfcheck` valida a localização do `choco.exe` por `ChocolateyInstall`,
`%ProgramData%\chocolatey\bin\choco.exe` ou `PATH`.

## Parsing

O Chocolatey tem opção `--limit-output`, também conhecida como `-r`, que reduz
as saídas de consulta para linhas separadas por `|`. O app centraliza esse
tratamento em `ChocolateyLimitedOutputParser` e ignora banners, warnings e
linhas sem separador.

Formatos usados no MVP:

```text
search/list: nome|versao
outdated: nome|versao_instalada|versao_disponivel|pinned
source list: nome|url|disabled|priority
```

## Versionamento

- `csproj`: `<Version>0.1.0</Version>`
- `app.json`: `"version": "0.1.0"`
- CLI `--version`: versão do assembly + `SdkVersion.Current`

## Testes

`tests/Ribanense.Solucoes.App.Chocolatey.Tests/` cobre:

- Search service e parser de saída limitada.
- List service com cruzamento de `outdated`.
- Install/upgrade/uninstall e composição de argumentos.
- Source service.
- Diagnóstico de `choco.exe`.

## Riscos conhecidos

- Algumas operações do Chocolatey exigem administrador, dependendo do pacote e
  da política da instalação local.
- `--limit-output` é mais estável que tabela textual, mas mensagens de warning
  ainda podem aparecer antes dos dados. O parser tolera essas linhas.
- A aba Fontes no MVP é propositalmente conservadora: lista e remove fontes; a
  inclusão com credenciais/prioridade deve ser projetada com UI própria.
