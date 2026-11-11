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

O Launcher usa a mesma tabela, com slug `launcher`. O executável de entrada tem nome estável (`Ribanense.Solucoes.Launcher.exe`) dentro da pasta. O `.exe` único de ~70 MB deixa de ser o asset novo. Quem ainda está nesse `.exe` recebe o zip completo na próxima atualização e passa a viver em `%LOCALAPPDATA%\Programs\Ribanense Soluções`.

## Como o cliente escolhe

1. Lê a versão instalada (`payload-manifest.json` ou `app.json`).
2. Procura uma corrente de deltas da versão instalada até a mais nova. Cada elo é o asset `delta-from-<versão-atual>.zip` da release seguinte.
3. Se algum elo faltar, se a corrente passar de 8 elos, ou se um arquivo do delta não bater com o hash, apaga o staging e baixa o zip completo.
4. Aplica em `<pasta>.tmp-*` (app) ou `<pasta>.new-*` (launcher) e só então troca a pasta. Se a troca falhar, a instalação atual continua no lugar.
5. No fim, confere o `payload-manifest.json` da versão nova contra os arquivos no disco.

`dados/`, `aplicativos/`, `portable.flag` e `install-mode.txt` não entram no manifesto. Uma atualização do launcher portátil não apaga os apps nem os dados que moram ao lado do executável.

## Geração

`ferramentas/payload-delta.ps1` é chamado por `publish-module.ps1` e `publish-launcher.ps1`. O `release.ps1` baixa o zip da tag anterior (quando ele existe) e passa `-PreviousDir` para calcular o delta. A primeira release no formato novo, e qualquer release cuja anterior ainda era um `.exe` único, sai sem delta.

A regra de diff é a mesma de `PayloadManifest` e `DeltaManifest` no PluginSDK.

## Ver também

- [`RELEASE_PROCESS.md`](RELEASE_PROCESS.md)
- [`ARQUITETURA.md`](ARQUITETURA.md)
- [`PARCEIROS.md`](PARCEIROS.md)
