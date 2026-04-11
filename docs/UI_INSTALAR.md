# UI do fluxo Instalar (winget)

Documenta o comportamento da vista **Instalar** (`InstallRunView`) — pesquisa, grelhas, botões **Cancelar** / **Instalar** — e do **rodapé só com log** no `MainWindow` (fluxo Instalar). Ver também [`UI_SHELL.md`](UI_SHELL.md) para a estrutura geral do layout e controlos reutilizáveis.

## Colunas da área principal

| Zona | Conteúdo |
|------|-----------|
| Esquerda | Grade de seleção. Quando o campo de busca está vazio ou o utilizador digita, mostra a **lista validada offline** (curada em `ItProfessionalsCatalog`), filtrada em tempo real via `FuzzyTextMatcher`. Ao pressionar **Enter** ou clicar em **Pesquisar**, mostra **resultados do repositório** (`winget search`). O rótulo acima da grade (`InstallCatalogModeLabel`) indica a origem. |
| Direita | **Nesta máquina / fila de instalação**: pacotes já instalados (filtrados winget/Loja) com **tom acizentado**; pacotes na fila a instalar com **destaque** (cores fortes). |

## Busca dual: offline e online

| Ação do utilizador | Comportamento |
|---------------------|---------------|
| Digitar no campo de busca | Filtro local imediato sobre a lista curada validada (`_offlineValidatedRows`), sem acesso à internet. Usa `FuzzyTextMatcher.IsFuzzyMatch` para correspondência aproximada. |
| Apagar todo o texto | Reexibe a lista curada completa. |
| Pressionar Enter ou clicar em Pesquisar | Executa `winget search` online. Os resultados substituem a grade, e o rótulo muda para "Resultados do repositório". |
| Query vazia + Buscar | Reexibe a lista curada. |

As duas fontes (offline validada e repositório online) são mantidas em coleções separadas internamente (`_offlineValidatedRows`, `_repositorySearchRows`), projetadas numa coleção visível única (`WingetCatalogSearchRows`).

A seleção (checkbox) é preservada ao alternar entre os dois modos via `_pendingInstall`.

## Instalação

- Comando: `IWingetPackageInstallService` → `winget install --id "<id>" -e --accept-package-agreements --accept-source-agreements` (e `--source` quando for `winget` ou `msstore`).
- O botão **Instalar** (na `InstallRunView`, por baixo das grelhas) executa a fila (pacotes ainda não presentes na listagem instalada), em sequência, e regista o resultado no log.

## Resumo e retry por similaridade

Ao final do lote de instalação, caso existam falhas, o sistema:

1. Acumula resultados em `_succeededInstalls` e `_failedInstalls`.
2. Para cada falha, procura candidatos alternativos via `winget search` + `WingetSearchRelevance.ScoreAgainstQuery`.
3. Exibe um **overlay-dialog de resumo** (`InstallRetrySummaryDialog`) com:
   - Total de sucessos e falhas.
   - Grade de candidatos de retry com checkbox de aprovação.
   - Ações: **Fechar**, **Tentar todos sugeridos**, **Revisar um por um**.

No modo "Tentar todos sugeridos", os candidatos aprovados são instalados sequencialmente. No modo "Revisar um por um", apenas os que o utilizador mantiver marcados são tentados, um de cada vez.

## Ações (`InstallRunView`)

Por baixo das duas grelhas, **numa única linha** e **alinhados à direita**: **Cancelar** e **Instalar** (com espaçamento entre eles), largura mínima ~**140px** cada.

## Rodapé do log (`MainWindow`)

O rodapé da janela contém **apenas** a linha de texto do log (sem rótulo). Padding horizontal alinhado ao conteúdo (**28px**); padding vertical do bloco reduzido para poupar espaço.

O **log de instalação** mostra no máximo **duas linhas** (altura fixa ~32px com `LineHeight` 16); texto longo é **truncado com reticências** (sem scroll horizontal). O conteúdo completo pode ser visto no **tooltip** ao pairar sobre o log.

Durante a instalação **não** se usa o overlay global (`IsLoading`): o log continua legível; o estado de instalação é `IsInstallingPackages`.

O lote a instalar é uma lista em memória (`toInstall`) criada no início de cada execução a partir de `_pendingInstall`; não há ficheiros temporários — o cancelamento usa `CancellationTokenSource` e encerra o processo `winget` em curso.

## Manutenção

- View models: `WingetCatalogPickRowViewModel` (pesquisa + checkbox), `WingetSearchRowViewModel` (`IsMutedInstalled` para linhas já no sistema), `RetryCandidateViewModel` (retry por similaridade), `InstallBatchResultEntry` (registo de resultado de lote).
- Estado da fila: `MainWindowViewModel._pendingInstall` (chave = ID do pacote).
- Coleções internas: `_offlineValidatedRows` (lista curada), `_repositorySearchRows` (resultados online), projetadas em `WingetCatalogSearchRows`.
