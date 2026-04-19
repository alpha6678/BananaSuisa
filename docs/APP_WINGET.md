# Gestor WinGet — app v0.2

Projeto: [`src/aplicativos/Ribanense.Solucoes.App.Winget/`](../src/aplicativos/Ribanense.Solucoes.App.Winget/).

App independente do catálogo Ribanense Soluções que oferece uma GUI sobre
`winget.exe` com **4 tabs**, detecção e reparo do próprio módulo, e uma busca
tolerante a linguagem natural usando dicionário curado + fuzzy matching.

## Tabs

| Tab | O que faz |
|-----|-----------|
| **Buscar** | Caixa de texto → `SearchEnhancer` normaliza, resolve aliases curados, roda `winget search` em paralelo para os Ids candidatos + a query bruta, deduplica por Id e reordena com aliases curados no topo. Botão "Instalar" por linha. |
| **Instalados** | `winget list --disable-interactivity` com parser da tabela (reconhece headers EN e PT). Botões "Atualizar" (só quando há versão disponível) e "Remover" por linha. |
| **Repositórios** | Gestão completa de `winget source`: listar, atualizar uma ou todas, adicionar via diálogo, remover (com confirmação), e restaurar padrão via UAC. |
| **Módulo** | Diagnóstico e reparo do próprio ecossistema winget/App Installer (ver abaixo). |

## Aba Módulo: diagnóstico e reparo

### Diagnóstico (`AppInstallerDiagnostics`)
Sem UAC. Para cada um dos pacotes relevantes:

- `winget.exe` — localizado via `WingetLocator` (LOCALAPPDATA + PATH) + `winget --version`.
- `Microsoft.DesktopAppInstaller` — via `Get-AppxPackage | ConvertTo-Json`.
- `Microsoft.VCLibs.140.00.UWPDesktop` — idem.
- `Microsoft.UI.Xaml.2.8` — idem.

Resultado em `AppInstallerStatus`: cada pacote com `Installed`, `Version`, `FullName`. Flag `Healthy` = tudo presente.

### Reparo (`AppInstallerRepair`) — requer UAC

Dois caminhos, ambos roteados via `IElevatedCommandRunner`:

1. **Reparar** (`ReregisterAsync`): sem rede. Para cada pacote presente, executa
   `Add-AppxPackage -DisableDevelopmentMode -Register <InstallLocation>\AppxManifest.xml`.
   Útil quando o registro está corrompido mas os pacotes existem.
2. **Baixar versão mais recente** (`DownloadAndInstallLatestAsync`): baixa o
   `.msixbundle` do App Installer de `https://aka.ms/getwinget` + VCLibs de
   `https://aka.ms/Microsoft.VCLibs.x64.14.00.Desktop.appx`, instala via
   `Add-AppxPackage`. Requer rede.

### Como a elevação funciona

`ElevatedCommandRunner` escreve um script PowerShell em `%TEMP%\ribanense-elev-<guid>.ps1`
envolvido com `Start-Transcript`/`Stop-Transcript` apontando para
`%TEMP%\ribanense-elev-<guid>.log`, dispara `Start-Process powershell.exe -Verb RunAs`
(que ativa o UAC), espera pelo término, lê o log, emite linha-a-linha via
`IProgress<string>` e deleta os arquivos temporários. Cancelamento do UAC retorna
`ElevatedResult(ExitCode=1223, Cancelled=true)` sem exception.

## Busca tolerante (`AliasAwareSearchEnhancer`)

### Pipeline

```
query crua
  → Similarity.Normalize         (lowercase, sem acentos, sem pontuação)
  → match exato em aliases.json  (synonym ou publicName)
  → fuzzy Jaro-Winkler ≥ 0.85    (até 5 candidatos extras)
  → IWingetSearchService(Id_i)   (em paralelo)
  → IWingetSearchService(query_crua) (fallback)
  → dedup por Id                 (aliases no topo, depois o resto)
  → top 30
```

### Dicionário curado

[`Resources/aliases.json`](../src/aplicativos/Ribanense.Solucoes.App.Winget/Resources/aliases.json)
(embutido como `EmbeddedResource`). Cada entrada tem:

```json
{
  "id": "Microsoft.VisualStudioCode",
  "publisher": "Microsoft",
  "publicName": "Visual Studio Code",
  "synonyms": ["code", "vs code", "vscode", "editor de codigo"],
  "category": "Desenvolvimento"
}
```

Versão inicial traz 40 apps populares em Desenvolvimento, Navegadores,
Produtividade, Comunicação, Utilitários, Mídia, Jogos e Segurança. Adicionar
novos: editar o JSON, rebuildar. Não precisa recompilar quem usa o app.

### Fuzzy matching

`Similarity` implementa Jaro-Winkler completo (~50 linhas, sem dependências).
"chorme" → `Google.Chrome` passa direto (score > 0.9). "xpto" sem match cai
para `winget search xpto` puro.

## CLI do app

Todo app Ribanense expõe:

```bat
Ribanense.Solucoes.App.Winget.exe --version        # JSON
Ribanense.Solucoes.App.Winget.exe --selfcheck      # verifica winget, exit 0/1
Ribanense.Solucoes.App.Winget.exe --logs [N]       # últimas N entradas do Winget.dat
```

`--logs` copia o `.dat` para `%TEMP%` antes de abrir (evita lock enquanto o app está rodando).

## Versionamento

- `csproj`: `<Version>0.2.0</Version>`
- `app.json`: `"version": "0.2.0"`
- CLI `--version`: `{"version":"0.2.0","sdk":"1.0.0"}`

`rb version` alerta se os três divergirem.

## Testes

`tests/Ribanense.Solucoes.App.Winget.Tests/` cobre:

- Parser de tabela (4 cases)
- Search/List services (6 cases)
- Install service (5 cases)
- **Source service** (10 cases) incluindo delegação do reset para `IElevatedCommandRunner`.
- **ElevatedCommandRunner** (6 cases) via `FakeProcessLauncher` — não dispara UAC real.
- **AppInstallerDiagnostics** (6 cases) — parse de `Get-AppxPackage` JSON.
- **AppInstallerRepair** (5 cases) — validação da composição dos scripts.
- **Similarity** (7 cases) — normalização + Jaro-Winkler.
- **EmbeddedAppAliasCatalog** (4 cases) — carga do aliases.json embutido.
- **AliasAwareSearchEnhancer** (7 cases) — match exato, fuzzy, fallback, dedup.

Total: **64 testes**.

## Riscos conhecidos

- Locale variado do winget: o parser tolera headers EN e PT (`Nome/Versão/Origem/Argumento/Tipo`). Se algum Windows emitir outros headers, adicionar alias no `ColumnIndex` do serviço correspondente.
- Latência da busca: 5 candidatos × ~500ms do `winget search` = até ~2.5s para queries sem cache. Threshold fuzzy 0.85 limita falsos-positivos.
- Cancelamento do UAC em operações do Módulo: tratado explicitamente com `ElevatedResult.Cancelled=true` + mensagem amigável na UI.
- `aliases.json` embutido: update exige rebuild. Evolução natural é mover para catálogo remoto ou plugar IA via `ISearchEnhancer`.
