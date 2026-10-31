# App Mago da Performance

App do catálogo que diagnostica e otimiza o desempenho do Windows: identifica
hardware e BIOS, ajusta registo e serviços, controla programas de arranque,
mede/liberta FPS, mede a qualidade da ligação de rede e faz limpeza — sempre com
backup e restauro das alterações.

## Como está empacotado

O app tem **três camadas**, e o PC do usuário não precisa de Python:

| Camada | Caminho | Papel |
|--------|---------|-------|
| Host (.exe) | [`Program.cs`](../src/aplicativos/Ribanense.Solucoes.App.Mago/Program.cs) | Contrato do Plugin SDK (`--version`, `--selfcheck`) e escolha do que abrir. |
| Executável standalone | `MagoDaPerformance.exe` (gerado no publish) | O app real, congelado pelo PyInstaller com Python + psutil embutidos. |
| Fonte (fallback de dev) | [`py/mago_da_performance.py`](../src/aplicativos/Ribanense.Solucoes.App.Mago/py/mago_da_performance.py) | Código do app; usado com Python instalado quando o standalone não existe. |
| Build do standalone | [`py/mago.spec`](../src/aplicativos/Ribanense.Solucoes.App.Mago/py/mago.spec) | Receita do PyInstaller chamada pelo publish. |
| Manifesto | [`app.json`](../src/aplicativos/Ribanense.Solucoes.App.Mago/app.json) | Contrato com o Launcher (id, versão, tag). |

O host procura `MagoDaPerformance.exe` na própria pasta; se não existir (build
de desenvolvimento), cai para o `.py`. Por isso mudanças de comportamento
acontecem quase sempre no `.py`, não no C#.

## Requisitos

**No PC do usuário:** Windows 10/11 x64. O Launcher já inclui o runtime dele.
O app precisa do .NET Desktop Runtime compartilhado; se faltar, o botão
**Instalar .NET** no catálogo baixa o pacote oficial da Microsoft e abre a janela
de progresso, com permissão de administrador. O card lista o Runtime e o
Desktop Runtime enquanto isso acontece. **Python não é necessário** — o release traz o
executável congelado.

**Na máquina de build:** .NET SDK (`global.json`) e Python 3.8+ com PyInstaller:

```bat
python -m pip install pyinstaller
```

## Contrato do SDK

| Argumento | Comportamento |
|-----------|---------------|
| `--version` | Imprime `{"version":"0.1.0","sdk":"1.0.0"}` e sai com 0. |
| `--selfcheck` | Com o standalone presente, executa `MagoDaPerformance.exe --selfcheck` e devolve o exit code dele. Sem ele, valida script + Python 3.8+ + pasta de dados. |
| (sem argumentos) | Cria a pasta de dados e abre o standalone (ou o script, no fallback). |

Exit codes do selfcheck: `0` ok, `2` executável/script ausente, `3` runtime Python
ausente (fallback) ou psutil/tkinter indisponível no standalone, `4` pasta de
dados inacessível, `5` falha ao executar o standalone.

O próprio `MagoDaPerformance.exe` aceita `--selfcheck` (sem abrir a interface e
sem UAC), o que permite testar o pacote em CI ou na mão.

## Dados e caminhos

O host respeita `RIBANENSE_APP_DATA` e `RIBANENSE_APP_HOME` (ver
[`PLUGIN_SDK.md`](PLUGIN_SDK.md)). O script usa a mesma ordem:
`RIBANENSE_APP_DATA` → `%LOCALAPPDATA%\MagoDaPerformance` (executável congelado
aberto direto) → pasta do script (execução de desenvolvimento).

| Arquivo | Conteúdo |
|---------|----------|
| `MagoDaPerformance_log.txt` | Log de operações do app. |
| `mago_backup.json` | Backup dos valores alterados (registo, serviços, arranque). |
| `mago_snapshots/` | Cópias datadas do backup. |

As preferências de FPS continuam em `%LOCALAPPDATA%\MagoDaPerformance\`, como no
script original.

## Elevação

O manifesto declara `requiresElevation: false`: o Launcher abre o app sem UAC.
Ao iniciar, o script confere se está como administrador e, se não estiver, se
reabre com `ShellExecute`/`runas` (o usuário vê o prompt do UAC). No executável
congelado ele reabre o próprio `.exe`; no modo script, reabre
`python.exe script.py`. Sem elevação, o app fecha com aviso — as operações
alteram `HKLM`, serviços e BCD.

## Teste de rede

A página **Rede** diagnostica a ligação sem ler texto localizado do `ipconfig`:

| Medida | Como é obtida |
|--------|---------------|
| Adaptadores ativos, IPv4, velocidade do link | `psutil.net_if_stats` / `net_if_addrs` |
| Gateway e DNS | `Get-CimInstance Win32_NetworkAdapterConfiguration` (propriedades em inglês) |
| Ping, jitter e perda | `ping -n 5 -w 1500`; o parser aceita `time=`, `tempo=`, `tiempo=` e `zeit=` |
| Download | `https://speed.cloudflare.com/__down` — até 25 MB, janela máxima de 15 s |
| Upload | `https://speed.cloudflare.com/__up` — 4 MB |

O download descarta os primeiros 750 ms para não medir a rampa do TCP/TLS; sem
isso, ligações rápidas parecem lentas. Cada teste completo transfere cerca de
29 MB, o que importa em ligações móveis.

O diagnóstico cruza três sinais: ping do router (rede local), ping da internet e
velocidade. Perda ou latência já no router aponta para Wi-Fi/cabo; router limpo
com internet lenta aponta para o ISP. Os resultados vão para o log da consola e
não são guardados no backup (são leituras, não alterações).

## Build, execução e release

```bat
rb.cmd compilar
rb.cmd app run Mago
rb.cmd publish Mago 0.1.0
rb.cmd release Mago 0.1.0
```

> Use a **versão posicional** no `publish`: a CLI atual encaminha `-Version x.y.z`
> como valor posicional (bug conhecido do `Invoke-AppPublish`), o que gera um zip
> com nome errado. `rb.cmd publish Mago 0.1.0` funciona.

O publish roda `dotnet publish`, depois o PyInstaller (`py\mago.spec`), e só então
gera o zip em `artifacts\publish\Mago\`:

```
mago-<versao>-win-x64.zip
  MagoDaPerformance.exe        (~13 MB, Python + psutil embutidos)
  Ribanense.Solucoes.App.Mago.exe
  py/mago_da_performance.py    (fallback de desenvolvimento)
  app.json
  icon.png
```

A tag de release é `mago-v<versao>` e o catálogo aponta para
`catalog/icons/mago.png`.

## Roadmap mobile

O app atual é profundamente Windows: registo (`winreg`/`reg`), serviços (`sc`),
BCD (`bcdedit`), WMI/PowerShell, processos e uma UI Tkinter. Nada disso roda em
Android/iOS como está.

O caminho para mobile é separar as camadas, não portar o arquivo inteiro:

1. **Núcleo portátil** (`core/`): coleta de hardware, catálogo de tweaks, plano de
   aplicação, backup/restauro e relatório — sem `tkinter`, `winreg` ou `ctypes`.
   Cada backend (Windows, Android, iOS) implementa as operações do sistema.
2. **UI desktop** continua Tkinter sobre esse núcleo.
3. **UI mobile** nova (Kivy/BeeWare ou nativa) consumindo o mesmo núcleo; limites
   de API no Android/iOS mudam bastante o que é permitido "otimizar".

Isso é um refactor grande e fica para uma fase própria. Enquanto o núcleo não for
extraído, qualquer versão mobile seria uma reescrita, não um reaproveitamento.

## Riscos e limites

- O app altera registo, serviços, arranque e BCD; exige administrador. O backup e
  o restauro ficam na pasta de dados; teste em máquina real antes de divulgar.
- O `.exe` onefile extrai para `%TEMP%` a cada início (1–3 s a mais). Se isso
  incomodar, o próximo passo é migrar para `--onedir`.
- A suíte de testes do script (`test_mago.py`, 60 testes) continua fora do
  monorepo; rodá-la exige apontar `SOURCE` para `py/mago_da_performance.py`.

## Ver também

- [`PLUGIN_SDK.md`](PLUGIN_SDK.md)
- [`RELEASE_PROCESS.md`](RELEASE_PROCESS.md)
- [`INDICE.md`](INDICE.md)
