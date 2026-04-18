# Plugin SDK

Contrato entre o **Launcher** e cada **app** do catálogo. Como os apps são `.exe` independentes, o contrato é um **protocolo** baseado em três pilares: manifesto, CLI e variáveis de ambiente.

## 1. Manifesto `app.json`

Cada app distribui um `app.json` na raiz da sua pasta.

```json
{
  "id": "com.ribanense.winget",
  "name": "Gestor WinGet",
  "publicName": "Gestor WinGet",
  "version": "1.0.0",
  "minimumLauncherVersion": "1.0.0",
  "entryExecutable": "Ribanense.Solucoes.App.Winget.exe",
  "icon": "icon.png",
  "category": "Pacotes",
  "requiresElevation": false,
  "githubTagPrefix": "winget-v"
}
```

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `id` | string | sim | Identificador global. Convenção: `com.ribanense.<slug>`. |
| `name` | string | sim | Nome interno/técnico. |
| `publicName` | string | sim | Nome exibido ao usuário (pode conter acentos). |
| `version` | SemVer | sim | Versão instalada. |
| `minimumLauncherVersion` | SemVer | sim | Versão mínima do Launcher requerida. |
| `entryExecutable` | path relativo | sim | `.exe` a ser iniciado. |
| `icon` | path relativo ou URL | não | Ícone exibido no card. |
| `category` | string | não | Agrupador no catálogo. |
| `requiresElevation` | bool | não | Se `true`, Launcher inicia com `runas`. |
| `githubTagPrefix` | string | sim | Prefixo de tag do release (ex.: `winget-v`). |

## 2. CLI obrigatório de cada app

Todo `.exe` de app deve suportar:

| Argumento | Comportamento |
|-----------|---------------|
| `--version` | Imprime JSON `{ "version": "x.y.z", "sdk": "x.y.z" }` em stdout, sai com código 0. Launcher usa para validar compatibilidade antes de abrir. |
| `--selfcheck` | Valida dependências nativas (DLLs, runtimes). Exit 0 = OK. Exit != 0 = problema; Launcher mostra em "Integridade". |
| (sem argumentos) | Abre a janela normalmente. |

Argumentos adicionais ficam a cargo do app. Launcher nunca repassa argumentos próprios.

## 3. Variáveis de ambiente injetadas pelo Launcher

Antes de `Process.Start`, o Launcher define:

| Variável | Valor |
|----------|-------|
| `RIBANENSE_APP_DATA` | `%LOCALAPPDATA%/Ribanense Soluções/apps/<id>/` — onde o app grava o `.dat` LiteDB e caches. |
| `RIBANENSE_APP_HOME` | Pasta da instalação do app (onde vive o `.exe`). |
| `RIBANENSE_LAUNCHER_PIPE` | Nome de um named pipe do Launcher (opcional) para log/telemetria. |

**Apps devem funcionar sem essas variáveis.** Quando ausentes:

- `RIBANENSE_APP_DATA` → fallback para `%LOCALAPPDATA%/Ribanense Soluções/apps/<id>/`.
- `RIBANENSE_APP_HOME` → fallback para `AppContext.BaseDirectory`.
- `RIBANENSE_LAUNCHER_PIPE` → log apenas local.

## 4. Versionamento do SDK

O SDK segue SemVer rígido.

- **Patch** (`1.0.1`): correções sem mudança de contrato.
- **Minor** (`1.1.0`): adições retrocompatíveis.
- **Major** (`2.0.0`): mudança incompatível; apps devem declarar `minimumLauncherVersion` apropriada, e o Launcher deve exibir aviso se a versão instalada do app exceder sua capacidade.

A constante canônica é `Ribanense.Solucoes.PluginSDK.SdkVersion.Current`.

## 5. Fallback seguro

- Se o Launcher está em versão inferior à `minimumLauncherVersion` do app, o Launcher **bloqueia** a abertura e sugere atualizar o Launcher.
- Se `app.json` for inválido, o app não aparece em "Meus apps".
- Se o `.exe` não corresponder a `entryExecutable`, Launcher marca como "Integridade comprometida" e pede reinstalação.

## Ver também

- [`ARQUITETURA.md`](ARQUITETURA.md)
- [`RELEASE_PROCESS.md`](RELEASE_PROCESS.md)
