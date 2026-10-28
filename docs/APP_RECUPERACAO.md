# Recuperação do Windows — app v0.1

Projeto: [`src/aplicativos/Ribanense.Solucoes.App.Recuperacao/`](../src/aplicativos/Ribanense.Solucoes.App.Recuperacao/).

App independente do catálogo Ribanense Soluções para **recuperar subsistemas do
Windows danificados por intervenção manual** (Windows enxugado com NTLite/tiny11/Atlas,
políticas corporativas, chaves/ACL apagadas à mão). O módulo inicial é **Áudio**
(porta nativa, em C#, do utilitário `Fix-WindowsAudio.ps1` — arquivo local de
referência, fora do repositório), com arquitetura de módulos para novos reparos
(rede, Windows Update, drivers etc.).

## Fases do módulo Áudio

| Fase | O que faz | Exige admin |
|------|-----------|-------------|
| **Diagnóstico** | Somente leitura: serviços (`Audiosrv`, `AudioEndpointBuilder`, `RpcSs`, `HDAudBus`, `hdaudio`), políticas de bloqueio (`DeviceInstall\Restrictions` cruzadas com os GUIDs MEDIA e AudioEndpoint), MMDevices (endpoints + ACL), arquivos base, PnP (controladores `CC_0401/0403`, classe MEDIA com códigos de problema, endpoints), Driver Store e diversos (setupapi.dev.log, internet, build). Salva `diagnostico.json` + `diagnostico.md` na pasta da execução. | Parcial (Driver Store requer) |
| **Backup** | Reativa `VSS`/`swprv` e remove política `DisableSR`; cria ponto de restauração com conferência real (contorna o limite de 1 ponto/24h liberando `SystemRestorePointCreationFrequency` temporariamente); exporta drivers (`DISM /Export-Driver`); exporta 6 chaves de registro via `reg.exe export`; grava `backup-manifest.json` que trava o Reparo. | Sim |
| **Reparo** | Restaura chaves de serviço de `ControlSet001/002` (`reg copy`); serviços em Automático; remove bloqueios de política de áudio (com confirmação); corrige ACL do MMDevices (3 passos: `SeTakeOwnershipPrivilege` → propriedade → ACEs por SID via handle `ChangePermissions`); reabilita dispositivos (problema 22); recria `hdaudio`; detecta codec ACPI sem driver (problema 28, ex. `ESSX8336`) e dispara `UsoClient StartScan`; SFC + DISM opcionais; revarredura; verificação final. NÃO apaga o Driver Store. | Sim |
| **Escalada** | Remoção cirúrgica de drivers MEDIA de terceiros (`pnputil /delete-driver /uninstall /force`) com backup de drivers obrigatório e guia do reparo in-place com ISO oficial. | Sim |
| **Instalar codec** | Instala driver de pasta local com `.inf` (`pnputil /add-driver /install`), revarre e verifica o codec + endpoints. | Sim |

## Elevação

O app roda sem elevação por padrão (diagnóstico funciona). Operações que mutam o
sistema reabrem o próprio executável elevado com `--run <fase>` (UAC), preservando
o fluxo — equivalente do bloco `[UAC]` do script. O banner do topo indica o estado
e oferece o botão **Reabrir como administrador**.

## Arquivos gerados

Tudo fica sob `%LOCALAPPDATA%\Ribanense Soluções\apps\com.ribanense.recuperacao\execucoes\<stamp>\`:

- `diagnostico.json` / `diagnostico.md` — relatório da fase 1.
- `backup\drivers-export\` + `*.reg` — export de drivers e chaves.
- `backup-manifest.json` — status por item (`OK`/`Ausente`/`Falhou`); o Reparo
  consulta o manifesto desta execução ou o mais recente anterior completo.
- `sfc-scannow.txt`, `dism-restorehealth.txt`, `escalada-remocoes.txt` — logs de processo.

## Conhecimento embutido (causas de "nenhum dispositivo de saída de áudio")

Constantes em `Recovery/Audio/AudioConstants.cs`:

- Classe MEDIA `{4d36e96c-e325-11ce-bfc1-08002be10318}`; AudioEndpoint `{cdf10ab0-12ef-11d0-9d0c-00c04fc9a425}`.
- SIDs: SYSTEM `S-1-5-18`, Administrators `S-1-5-32-544`, Users `S-1-5-32-545`, OWNER RIGHTS `S-1-3-0` (cadeado contra o próprio dono).
- Códigos de problema PnP: 22 (desabilitado), 28 (sem driver).
- 7 arquivos base (detectam imagem removida/enxugada).
- ACL do MMDevices: SYSTEM precisa de FullControl; a correção usa handle .NET com `ChangePermissions` porque o `Set-Acl` falha quando a DACL não concede WRITE_DAC a ninguém.

## CLI do app

```bat
Ribanense.Solucoes.App.Recuperacao.exe --version
Ribanense.Solucoes.App.Recuperacao.exe --selfcheck
Ribanense.Solucoes.App.Recuperacao.exe --logs 100
Ribanense.Solucoes.App.Recuperacao.exe --run Diagnostico   (também: Backup, Reparo, Escalada, Codec)
```

`--run` é usado internamente pelo relançamento elevado para auto-executar a fase.

## Versionamento

- `csproj`: `<Version>0.1.0</Version>`
- `app.json`: `"version": "0.1.0"`, tag `recuperacao-v`

## Testes

`tests/Ribanense.Solucoes.App.Recuperacao.Tests/` cobre:

- Parser JSON do `PowerShellProbe` (array, objeto único, vazio, ruído antes do JSON, datas `/Date(epoch)/`).
- `RunContext`: criação de pasta, roundtrip do manifesto (tolerante a PascalCase/camelCase), busca da execução mais recente com manifesto.
- Nomes de passo do reparo com/sem SFC/DISM; helpers de backup (`Quote`, `UltimaLinha`, busca de export de drivers).
- Constantes (GUIDs/SIDs/arquivos) e scripts PowerShell embutidos (filtros MEDIA/endpoints).

## Limitações e validação manual

- As fases 2–5 mexem em serviços, registro, drivers e imagem do sistema: **validação
  real exige a máquina física com privilégios elevados** — os testes cobrem apenas
  as partes puras (parsing, modelos, scripts).
- `Get-WindowsDriver`/`Get-PnpDevice` via PowerShell 5.1 embutido: saída sujeita a
  localização do Windows (a desserialização é por propriedade, não por rótulo).
- Reparo in-place (ISO oficial) permanece manual, guiado pela fase Escalada.
