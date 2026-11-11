# App Mago da Performance

O Mago da Performance é um **projeto parceiro**. O código, o instalador e as releases ficam em [Nickallafe/Mago](https://github.com/Nickallafe/Mago). Este repositório não compila nem publica o app.

O card no launcher vem de [`catalog/parceiros.json`](../catalog/parceiros.json):

| | |
|--|--|
| Repositório | `Nickallafe/Mago` |
| Tags | `v<semver>` (ex.: `v0.2.9`) |
| Instalador | `MagoSetup-<versão>.exe` + `.sha256` |
| Portátil | `MagoDaPerformance.exe`, quando a release publicar |
| Pasta típica do setup | `%LOCALAPPDATA%\Programs\Mago da Performance` |

**Instalar** no card prefere o executável portátil e, se a release não tiver esse arquivo, baixa o setup, confere o SHA256 e roda em silêncio. **No Windows** prefere o setup quando os dois assets existem. O Mago pede elevação (UAC) ao abrir; o setup em si instala sem administrador.

A versão que o card mostra é a da release do Nick, não uma cópia local. O clone de desenvolvimento, se existir, fica em `externos/mago` (`rb parceiros sync`) e não entra no build.

As tags antigas `mago-v*` deste repositório continuam no GitHub e não recebem versão nova.

## Ver também

- [`PARCEIROS.md`](PARCEIROS.md)
- [`ATUALIZACOES_INCREMENTAIS.md`](ATUALIZACOES_INCREMENTAIS.md)
