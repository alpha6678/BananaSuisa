# UI do fluxo Instalar (winget)

Documenta o comportamento da vista **Instalar** (`InstallRunView`) — pesquisa, grelhas, botões **Cancelar** / **Instalar** — e do **rodapé só com log** no `MainWindow` (fluxo Instalar). Ver também [`UI_SHELL.md`](UI_SHELL.md) para a estrutura geral do layout e controlos reutilizáveis.

## Colunas da área principal

| Zona | Conteúdo |
|------|-----------|
| Esquerda | Resultados de **Pesquisar** no catálogo (`winget search`). Cada linha tem **caixa de seleção**; marcar adiciona o pacote à **fila** (persiste entre pesquisas). |
| Direita | **Nesta máquina / fila de instalação**: pacotes já instalados (filtrados winget/Loja) com **tom acizentado**; pacotes na fila a instalar com **destaque** (cores fortes). |

## Instalação

- Comando: `IWingetPackageInstallService` → `winget install --id "<id>" -e --accept-package-agreements --accept-source-agreements` (e `--source` quando for `winget` ou `msstore`).
- O botão **Instalar** (na `InstallRunView`, por baixo das grelhas) executa a fila (pacotes ainda não presentes na listagem instalada), em sequência, e regista o resultado no log.

## Ações (`InstallRunView`)

Por baixo das duas grelhas, **numa única linha** e **alinhados à direita**: **Cancelar** e **Instalar** (com espaçamento entre eles), largura mínima ~**140px** cada.

## Rodapé do log (`MainWindow`)

O rodapé da janela contém **apenas** a linha de texto do log (sem rótulo). Padding horizontal alinhado ao conteúdo (**28px**); padding vertical do bloco reduzido para poupar espaço.

O **log de instalação** mostra no máximo **duas linhas** (altura fixa ~32px com `LineHeight` 16); texto longo é **truncado com reticências** (sem scroll horizontal). O conteúdo completo pode ser visto no **tooltip** ao pairar sobre o log.

Durante a instalação **não** se usa o overlay global (`IsLoading`): o log continua legível; o estado de instalação é `IsInstallingPackages`.

O lote a instalar é uma lista em memória (`toInstall`) criada no início de cada execução a partir de `_pendingInstall`; não há ficheiros temporários — o cancelamento usa `CancellationTokenSource` e encerra o processo `winget` em curso.

## Manutenção

- View models: `WingetCatalogPickRowViewModel` (pesquisa + checkbox), `WingetSearchRowViewModel` (`IsMutedInstalled` para linhas já no sistema).
- Estado da fila: `MainWindowViewModel._pendingInstall` (chave = ID do pacote).
