# Ambiente de desenvolvimento

Requisitos para trabalhar em **Ribanense Soluções**.

## Requisitos obrigatórios

| Categoria | Requisito | Notas |
|-----------|-----------|-------|
| Sistema operacional | Windows 10 ou 11 | Alvo do produto. |
| .NET SDK | 10.0.x | Alinhado com [`global.json`](../global.json). |
| Shell | PowerShell 5.1 (Windows) ou PowerShell 7 (`pwsh`) | `Ribanense.cmd` prefere `pwsh` se estiver no PATH. |
| Git | Qualquer versão moderna | Necessário para tags e releases. |

## Requisitos opcionais

| Ferramenta | Uso |
|------------|-----|
| `gh` (GitHub CLI) | Publicação de releases via `rb.cmd release`. [Instalação](https://cli.github.com/). |
| `pwsh` | Shell moderno; melhora a execução do CLI. |
| Visual Studio 2026 / Rider / VS Code | Edição .NET com debugger WPF. |
| Pester | Testes PowerShell quando aplicáveis. |

## Comandos básicos

```bat
.\rb.cmd help
.\rb.cmd compilar
.\rb.cmd run
.\rb.cmd test
.\rb.cmd check
```

Equivalentes diretos em `dotnet`:

```powershell
dotnet build .\Ribanense.Solucoes.slnx
dotnet run --project .\src\Ribanense.Solucoes.Launcher\Ribanense.Solucoes.Launcher.csproj
dotnet test  .\Ribanense.Solucoes.slnx
```

## Pastas geradas em runtime

| Caminho | Conteúdo |
|---------|----------|
| `bin/`, `obj/` | Saída de build por projeto; já no `.gitignore`. |
| `artifacts/publish/<App>/` | Zip + SHA256 + `app.json` de publicações locais. |
| `%LOCALAPPDATA%\Ribanense Solucoes\apps\<id>\` | Dados do app em runtime (LiteDB `<Nome>.dat`, caches). |

## Setup inicial

1. Clonar o repositório.
2. Confirmar `dotnet --version` mostrando 10.x.
3. `.\rb.cmd compilar` para baixar pacotes NuGet e validar build.
4. `.\rb.cmd run` para abrir o Launcher.
5. (Opcional) `gh auth login` se for publicar releases.

## Problemas comuns

| Sintoma | Ação |
|---------|------|
| `.\rb.cmd` não executa | Confirmar diretório correto e PowerShell disponível. |
| Build falha em `dotnet restore` | Confirmar SDK 10.x e acesso ao `nuget.org`. |
| Launcher não abre após `rb.cmd run` | Ver `bin/Debug/net10.0-windows/` do projeto Launcher; conferir logs no Event Viewer. |
| `gh release create` falha | Rodar `gh auth status`; re-autenticar com `gh auth login`. |

## Ver também

- [`../CONTRIBUTING.md`](../CONTRIBUTING.md)
- [`FERRAMENTAS_CLI.md`](FERRAMENTAS_CLI.md)
- [`ARQUITETURA.md`](ARQUITETURA.md)
