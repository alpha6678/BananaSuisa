# Atualizações incrementais

O Launcher e os apps Windows publicados por este repositório não baixam o pacote inteiro quando a instalação atual ainda está íntegra. Cada release traz o zip completo (primeira instalação e fallback) e um delta a partir da versão imediatamente anterior.

## O que vai na release

| Asset | Papel |
|-------|-------|
| `<slug>-<versão>-win-x64.zip` | Payload completo. Primeira instalação e fallback. |
| `<slug>-<versão>-win-x64.zip.sha256` | SHA256 do zip completo. |
| `payload-manifest.json` | Lista `path`, `size` e `sha256` de cada arquivo da versão. |
| `delta-from-<anterior>.zip` | Só os arquivos adicionados ou alterados, mais o `payload-manifest.json` novo. |
| `delta-from-<anterior>.json` | Manifesto do delta: arquivos novos/alterados e a lista `deleted`. |
| `delta-from-<anterior>.zip.sha256` | SHA256 do zip de delta. |

O Launcher usa a mesma tabela e mais três assets: `launcher-<versão>-win-x64.exe` (o `.exe` único, que é o bootstrap com a pasta embutida), `RibanenseSolucoesSetup-<versão>.exe` (setup que baixa a versão mais recente) e os `.sha256` deles.

## Como o cliente escolhe

A cópia instalada é uma **pasta** com `payload-manifest.json`. A cópia avulsa é o **`.exe` único**.

| Situação | O que baixa na atualização |
|----------|----------------------------|
| Cópia instalada (pasta, com manifesto) | Corrente de deltas; zip completo só se faltar elo ou hash |
| `.exe` único, sem instalar | O `.exe` único novo, inteiro (como sempre foi) |
| Cópia portátil (pasta com `portable.flag`) | Mesmo que a instalada: deltas |

Fluxo da pasta:

1. Lê a versão instalada (`payload-manifest.json` ou `app.json`).
2. Procura uma corrente de deltas da versão instalada até a mais nova. Cada elo é o asset `delta-from-<versão-atual>.zip` da release seguinte.
3. Se algum elo faltar, se a corrente passar de 8 elos, ou se um arquivo do delta não bater com o hash, apaga o staging e baixa o zip completo.
4. Aplica em `<pasta>.tmp-*` (app) ou `<pasta>.new-*` (launcher) e só então troca a pasta. Se a troca falhar, a instalação atual continua no lugar.
5. No fim, confere o `payload-manifest.json` da versão nova contra os arquivos no disco.

`dados/`, `aplicativos/`, `portable.flag` e `install-mode.txt` não entram no manifesto. Uma atualização do launcher portátil não apaga os apps nem os dados que moram ao lado do executável.

## O `.exe` único e a instalação

O `.exe` único é um **bootstrap** ([`src/Ribanense.Solucoes.LauncherBootstrap`](../src/Ribanense.Solucoes.LauncherBootstrap/Program.cs)) que carrega a pasta do launcher dentro de si como `payload.zip`. Ao rodar:

1. extrai a pasta para `%LOCALAPPDATA%\Ribanense Soluções\runtime\<versão>\`;
2. abre o launcher de verdade a partir dali.

Por isso rodar e instalar **não baixam nada**. Com `--install`, ele copia a pasta já extraída para `%LOCALAPPDATA%\Programs\Ribanense Soluções` (ou para a pasta indicada por `--from`), cria o atalho e registra em Aplicativos e recursos. A partir daí a cópia instalada passa a atualizar por delta.

O `.exe` único continua trocando o arquivo inteiro quando é ele mesmo que se atualiza, porque nesse formato não há pasta para comparar. Instalar é que muda a regra.

## O setup

`RibanenseSolucoesSetup-<versão>.exe` é um executável self-contained pequeno, sem o launcher dentro. Ao rodar:

1. consulta a release `launcher-v*` mais recente;
2. baixa `launcher-<versão>-win-x64.exe` e o `.sha256`, e confere o hash;
3. executa o `.exe` único com `--install`.

Como o conteúdo vem da release, o setup nunca fica velho. Ele também aceita releases antigas, que só publicam `launcher-<versão>-win-x64.zip`: nesse caso baixa o zip, extrai e instala a partir da pasta.

## Geração

`ferramentas/payload-delta.ps1` é chamado por `publish-module.ps1` e `publish-launcher.ps1`. O `release.ps1` baixa o zip da tag anterior (quando ele existe) e passa `-PreviousDir` para calcular o delta. A primeira release no formato novo, e qualquer release cuja anterior ainda era um `.exe` único, sai sem delta.

Na prática, um delta do launcher entre duas versões de correção fica na casa das dezenas de KB, contra ~64 MB do zip completo.

A regra de diff é a mesma de `PayloadManifest` e `DeltaManifest` no PluginSDK.

## Ver também

- [`RELEASE_PROCESS.md`](RELEASE_PROCESS.md)
- [`ARQUITETURA.md`](ARQUITETURA.md)
- [`PARCEIROS.md`](PARCEIROS.md)
