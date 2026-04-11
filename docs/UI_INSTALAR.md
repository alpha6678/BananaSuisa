# UI do fluxo Instalar (winget)

Documenta o comportamento da vista **Instalar** (`InstallRunView`) e da faixa de ações junto ao **log de instalação** no `MainWindow`.

## Colunas da área principal

| Zona | Conteúdo |
|------|-----------|
| Esquerda | Resultados de **Pesquisar** no catálogo (`winget search`). Cada linha tem **caixa de seleção**; marcar adiciona o pacote à **fila** (persiste entre pesquisas). |
| Direita | **Nesta máquina / fila de instalação**: pacotes já instalados (filtrados winget/Loja) com **tom acizentado**; pacotes na fila a instalar com **destaque** (cores fortes). |

## Instalação

- Comando: `IWingetPackageInstallService` → `winget install --id "<id>" -e --accept-package-agreements --accept-source-agreements` (e `--source` quando for `winget` ou `msstore`).
- O botão **Instalar** executa a fila (pacotes ainda não presentes na listagem instalada), em sequência, e regista o resultado no log.

## Rodapé do log (`MainWindow`)

O **log de instalação** mostra no máximo **duas linhas** (altura fixa ~32px com `LineHeight` 16); texto longo é **truncado com reticências** (sem scroll). O conteúdo completo pode ser visto no **tooltip** ao pairar sobre o log.

Por baixo do log, **alinhados à direita**: **Cancelar** (em cima) e **Instalar** (em baixo), largura mínima ~**140px** cada.

Durante a instalação **não** se usa o overlay global (`IsLoading`): o log continua legível; o estado de instalação é `IsInstallingPackages`.

O lote a instalar é uma lista em memória (`toInstall`) criada no início de cada execução a partir de `_pendingInstall`; não há ficheiros temporários — o cancelamento usa `CancellationTokenSource` e encerra o processo `winget` em curso.

## Manutenção

- View models: `WingetCatalogPickRowViewModel` (pesquisa + checkbox), `WingetSearchRowViewModel` (`IsMutedInstalled` para linhas já no sistema).
- Estado da fila: `MainWindowViewModel._pendingInstall` (chave = ID do pacote).
