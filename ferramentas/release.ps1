#Requires -Version 5.1
<#
.SYNOPSIS
  Cria tag Git e publica GitHub Release para um app do monorepo.

.DESCRIPTION
  Executa publish-module.ps1, deriva o prefixo de tag do nome do app
  (convencao: <nome-minusculo>-v<semver>), cria a tag e publica o release
  via gh (GitHub CLI), anexando zip + sha256 + app.json.

.PARAMETER App
  Nome do app conforme sufixo do projeto (ex: "Winget").

.PARAMETER Version
  Versao SemVer do release.

.PARAMETER TagPrefix
  Sobrescreve o prefixo de tag. Default: o nome do app em minusculas + "-v".

.EXAMPLE
  .\release.ps1 -App Winget -Version 1.0.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $App,
    [Parameter(Mandatory = $true)] [string] $Version,
    [string] $TagPrefix
)

$ErrorActionPreference = 'Stop'
$ScriptRoot = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$ProjectRoot = Split-Path -Parent $ScriptRoot

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI (gh) nao encontrado no PATH. Instale em https://cli.github.com/."
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw "git nao encontrado no PATH."
}

if (-not $TagPrefix) { $TagPrefix = "$($App.ToLowerInvariant())-v" }
$tag = "$TagPrefix$Version"

Push-Location $ProjectRoot
try {
    $existing = & git tag --list $tag
    if ($existing) {
        throw "Tag '$tag' ja existe. Remova (git tag -d $tag) ou use outra versao."
    }

    & "$ScriptRoot\publish-module.ps1" -App $App -Version $Version
    if ($LASTEXITCODE -ne 0) { throw "publish-module.ps1 falhou." }

    $outDir = Join-Path $ProjectRoot "artifacts\publish\$App"
    $lowerApp = $App.ToLowerInvariant()
    $zipPath = Join-Path $outDir "$lowerApp-$Version-win-x64.zip"
    $shaPath = "$zipPath.sha256"
    $manifestPath = Join-Path $outDir 'app.json'

    if (-not (Test-Path -LiteralPath $zipPath)) { throw "Zip nao encontrado: $zipPath" }
    if (-not (Test-Path -LiteralPath $shaPath)) { throw "SHA256 nao encontrado: $shaPath" }

    Write-Host "Criando tag $tag..." -ForegroundColor Cyan
    & git tag $tag
    if ($LASTEXITCODE -ne 0) { throw "git tag falhou." }

    Write-Host "Publicando release $tag..." -ForegroundColor Cyan
    $args = @('release', 'create', $tag, $zipPath, $shaPath, '--title', "$App $Version", '--notes', "Release automatizado de $App $Version.")
    if (Test-Path -LiteralPath $manifestPath) { $args += $manifestPath }

    & gh @args
    if ($LASTEXITCODE -ne 0) { throw "gh release create falhou." }

    Write-Host ""
    Write-Host "Release $tag publicado." -ForegroundColor Green
}
finally {
    Pop-Location
}
