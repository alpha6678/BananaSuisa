#Requires -Version 5.1
# BananaSuisa.cli.ps1 — interface de linha de comandos para tarefas de desenvolvimento

param(
    [Parameter(Position = 0)]
    [string] $Command = 'help',

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Rest
)

$ErrorActionPreference = 'Stop'

$script:CliRoot = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$script:ProjectRoot = Split-Path -Parent $script:CliRoot

function Show-BananaSuisaCliHelp {
    Write-Host @'
BananaSuisa CLI — tarefas de desenvolvimento

Comandos:
  build, gerar     Consolida modulos em BananaSuisa.ps1 (Gerar_BananaSuisa.ps1)
  versao           Mostra a versao definida em nucleo/versao.ps1
  help             Esta ajuda

Exemplos:
  .\ferramentas\BananaSuisa.cli.ps1 build
  .\ferramentas\BananaSuisa.cmd build
  .\bs.cmd gerar

'@
}

switch -Regex ($Command.ToLowerInvariant()) {
    '^(build|gerar)$' {
        $gerar = Join-Path $script:CliRoot 'Gerar_BananaSuisa.ps1'
        if (-not (Test-Path -LiteralPath $gerar)) {
            throw "Script de build nao encontrado: $gerar"
        }
        & $gerar
    }
    '^versao$' {
        $versaoPath = Join-Path $script:ProjectRoot 'BananaSuisa_desenvolvimento\nucleo\versao.ps1'
        if (-not (Test-Path -LiteralPath $versaoPath)) {
            throw "Arquivo de versao nao encontrado: $versaoPath"
        }
        . $versaoPath
        Write-Output $script:BananaSuisaVersao
    }
    '^help$|^(\?|-h|--help)$' {
        Show-BananaSuisaCliHelp
    }
    default {
        Write-Warning "Comando desconhecido: $Command"
        Show-BananaSuisaCliHelp
        exit 1
    }
}
