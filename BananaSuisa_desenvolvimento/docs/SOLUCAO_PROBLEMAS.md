# Solucao de problemas

Este documento reune sintomas comuns do BananaSuisa atual e os caminhos de diagnostico mais uteis.

## Tabela rapida

| Sintoma | Causa provavel | Acao sugerida |
|---------|----------------|---------------|
| `winget` nao encontrado | App Installer ausente, corrompido ou fora do path | Verificar Store, `WindowsApps` e reparo em `funcionalidades/actions.ps1`. |
| Catalogo vazio ou inconsistente | `BananaSuisa_recursos` fora da raiz correta ou memoria em estado invalido | Confirmar `ProjectRoot`, `PayloadRoot` e estrutura de pastas. |
| Aplicacao abre mas nao instala | Runtime do `winget`, permissao ou erro de processo | Rever logs, codigo de saida e executar como administrador. |
| Dados antigos nao aparecem | Migracao de memoria nao ocorreu como esperado | Conferir pastas antigas e destino atual em `BananaSuisa_recursos\BananaSuisa_memoria`. |
| App congela durante operacao | Operacao longa, leitura de processo ou carga de UI | Verificar log, contexto do fluxo e limites da UI PowerShell/WinForms. |
| `npx` ou MCP Playwright nao funcionam | Node.js ausente ou Cursor nao reiniciado | Rever `docs/FERRAMENTAS_IA.md` e `docs/AMBIENTE.md`. |

## WinGet nao encontrado

Verifique nesta ordem:

1. Se o **App Installer** esta instalado pela Microsoft Store.
2. Se `winget.exe` existe em `%LOCALAPPDATA%\Microsoft\WindowsApps\`.
3. Se o reparo integrado do BananaSuisa foi executado.
4. Se a sessao atual tem acesso ao path esperado.

Pontos do codigo relacionados:

- `nucleo/bootstrap.ps1`
- `funcionalidades/actions.ps1`

Documentacao de apoio:

- [`../especialistas/uwp_appinstaller.md`](../especialistas/uwp_appinstaller.md)
- [`../especialistas/winget_exit_codes.md`](../especialistas/winget_exit_codes.md)

## Catalogo vazio, errado ou desatualizado

Confirme que:

- `BananaSuisa_recursos` esta na raiz do projeto, no mesmo nivel de `BananaSuisa.ps1`;
- a memoria ativa esta em `BananaSuisa_recursos\BananaSuisa_memoria`;
- os ficheiros JSON esperados existem em `Dados/`;
- o log contem valores coerentes para `ProjectRoot` e `PayloadRoot`.

Local do log:

- `BananaSuisa_recursos\BananaSuisa_memoria\Registros\BananaSuisa.json`

## Instalacao, update ou remocao falham

Verifique:

- se a aplicacao foi executada como administrador;
- se o `winget` devolveu codigo de erro conhecido;
- se houve erro em `stderr`;
- se a rede ou fonte do pacote estavam acessiveis;
- se o App Installer e o ecossistema UWP estavam saudaveis.

Documentacao de apoio:

- [`FLUXO_INSTALACAO.md`](FLUXO_INSTALACAO.md)
- [`../especialistas/winget_exit_codes.md`](../especialistas/winget_exit_codes.md)

## Migracao de dados antigos

Se ainda existir:

- `%LOCALAPPDATA%\BananaSuisa_memoria`
- ou `BananaSuisa_memoria` na raiz antiga do projeto

o app tenta mover esse conteudo para:

- `BananaSuisa_recursos\BananaSuisa_memoria`

Isto acontece quando a pasta nova ainda nao existe e o bootstrap encontra dados legados.

## App congela ou parece travado

O produto atual usa PowerShell + WinForms com leitura de processo em loop e `DoEvents()`.

Isto significa que:

- parte da responsividade depende do comportamento do processo externo;
- operacoes longas podem parecer congelamento mesmo com a janela aberta;
- problemas de rede, App Installer ou `winget` podem refletir diretamente na experiencia visual.

Documentacao de apoio:

- [`../especialistas/powershell_ui.md`](../especialistas/powershell_ui.md)
- [`FLUXO_INSTALACAO.md`](FLUXO_INSTALACAO.md)

## Permissao e ambiente

Alguns fluxos exigem:

- privilegios elevados;
- acesso a internet;
- permissao de escrita em `BananaSuisa_recursos\BananaSuisa_memoria`;
- runtime Windows compativel com App Installer, AppX e `winget`.

Se o comportamento estiver estranho logo no setup, revisar:

- [`../../docs/AMBIENTE.md`](../../docs/AMBIENTE.md)
- [`../../docs/FERRAMENTAS_CLI.md`](../../docs/FERRAMENTAS_CLI.md)
- [`../../docs/FERRAMENTAS_IA.md`](../../docs/FERRAMENTAS_IA.md)

## Quando abrir issue ou reportar regressao

Leve sempre:

- versao do Windows;
- versao do PowerShell;
- se executou como administrador;
- codigo de saida do `winget`, se existir;
- recorte do log relevante;
- passos exatos para reproduzir.
