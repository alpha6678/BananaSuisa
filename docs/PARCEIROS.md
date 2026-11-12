# Projetos parceiros

Um projeto parceiro não mora neste monorepo. O launcher mostra o card, lê a versão publicada no repositório de quem mantém o projeto e instala o asset que essa pessoa publicou. Não há cópia do código aqui e o `rb publish all` não gera release para esse id.

A lista fica em [`catalog/parceiros.json`](../catalog/parceiros.json). O launcher baixa esse arquivo do mesmo jeito que o `catalog.json` (raw no GitHub, cache no vault e cópia embutida no `.exe`). Se o mesmo `id` existir nos dois catálogos, o parceiro vence.

## Campos

| Campo | Papel |
|-------|-------|
| `id`, `name`, `publicName`, `description`, `category`, `icon` | O card. |
| `githubOwner`, `githubRepo`, `githubTagPrefix` | Onde estão as releases. O Mago usa o prefixo `v` (`v0.2.9`). |
| `installerAsset` / `installerShaAsset` | Padrão do instalador, com `{version}`. Ex.: `MagoSetup-{version}.exe`. |
| `portableAsset` / `portableShaAsset` | Executável portátil, quando a release publicar um. |
| `entryExecutable` | Nome do `.exe` dentro da pasta portátil. |
| `installPathCandidates` | Pastas para achar uma cópia já instalada. Aceita `%LOCALAPPDATA%`. |
| `uninstallRegistryName` | `DisplayName` em `HKCU\...\Uninstall` para achar a pasta e a versão. |
| `localPath` | Pasta do clone de desenvolvimento, relativa à raiz do repo. Não entra no launcher do usuário. |

## Como o launcher instala

1. **Portátil**, se a release tiver o asset portátil: grava o `.exe` em `aplicativos/<Nome>/`, valida o SHA256 quando o arquivo `.sha256` existe e escreve um `app.json` para o card reconhecer a versão.
2. **Instalador**, se não houver portátil (ou se o usuário escolher **No Windows** e o setup existir): baixa o setup, exige o `.sha256`, confere o hash e executa `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS`.
3. A versão instalada é lida, nesta ordem: `app.json` da pasta do launcher, `DisplayVersion` do registro, saída de `--version` (timeout de 4 s) e `FileVersion` do executável.

O Mago da Performance, em `Nickallafe/Mago`, hoje publica `MagoSetup-<versão>.exe` e o `.sha256`. O portátil `MagoDaPerformance.exe` saiu pela última vez na `v0.2.1`. Enquanto a release nova não trouxer o portátil, o launcher usa o setup. O próprio Mago continua se atualizando pelo updater dele; este launcher não reimplementa esse updater.

## Na máquina de quem desenvolve

```bat
rb parceiros list
rb parceiros sync
rb parceiros status
```

`sync` clona ou atualiza (`git pull --ff-only`) em `externos/<slug>`. Essa pasta está no `.gitignore`. `status` mostra a última release do GitHub e o commit local.

## Diferença para os apps do monorepo

Os apps deste repositório atualizam por delta (só o que mudou). Um projeto parceiro não tem manifesto nem delta nosso: o launcher baixa o asset que o parceiro publicou, confere o `.sha256` e executa. O formato do asset é escolha de quem mantém o projeto.

## Ver também

- [`APP_MAGO.md`](APP_MAGO.md)
- [`ATUALIZACOES_INCREMENTAIS.md`](ATUALIZACOES_INCREMENTAIS.md)
- [`FERRAMENTAS_CLI.md`](FERRAMENTAS_CLI.md)
