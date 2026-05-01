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

if (-not $TagPrefix) {
    if ($App -ieq 'Launcher') {
        $TagPrefix = 'launcher-v'
    } else {
        $TagPrefix = "$($App.ToLowerInvariant())-v"
    }
}
$tag = "$TagPrefix$Version"

Push-Location $ProjectRoot
try {
    $existing = & git tag --list $tag
    if ($existing) {
        throw "Tag '$tag' ja existe. Remova (git tag -d $tag) ou use outra versao."
    }

    $isLauncher = $App -ieq 'Launcher'
    if ($isLauncher) {
        & "$ScriptRoot\publish-launcher.ps1" -Version $Version
    } else {
        & "$ScriptRoot\publish-module.ps1" -App $App -Version $Version
    }
    if ($LASTEXITCODE -ne 0) {
        throw $(if ($isLauncher) { 'publish-launcher.ps1 falhou.' } else { 'publish-module.ps1 falhou.' })
    }

    $outDir = Join-Path $ProjectRoot "artifacts\publish\$App"
    $lowerApp = $App.ToLowerInvariant()
    $zipBaseName = if ($isLauncher) { "launcher-$Version-win-x64.zip" } else { "$lowerApp-$Version-win-x64.zip" }
    $zipPath = Join-Path $outDir $zipBaseName
    $shaPath = "$zipPath.sha256"
    $manifestPath = Join-Path $outDir 'app.json'

    if (-not (Test-Path -LiteralPath $zipPath)) { throw "Zip nao encontrado: $zipPath" }
    if (-not (Test-Path -LiteralPath $shaPath)) { throw "SHA256 nao encontrado: $shaPath" }

    Write-Host "Criando tag $tag..." -ForegroundColor Cyan
    & git tag $tag
    if ($LASTEXITCODE -ne 0) { throw "git tag falhou." }

    $releaseTitle = if ($isLauncher) {
        "Ribanense Soluções Launcher $Version"
    } else {
        "$App $Version"
    }
    $releaseNotes = if ($isLauncher) {
        "Release automatizado do Launcher $Version."
    } else {
        "Release automatizado de $App $Version."
    }

    Write-Host "Publicando release $tag..." -ForegroundColor Cyan
    $ghArgs = @('release', 'create', $tag, $zipPath, $shaPath, '--title', $releaseTitle, '--notes', $releaseNotes)
    if (-not $isLauncher -and (Test-Path -LiteralPath $manifestPath)) { $ghArgs += $manifestPath }

    & gh @ghArgs
    if ($LASTEXITCODE -ne 0) { throw "gh release create falhou." }

    Write-Host "Enviando tag para origin..." -ForegroundColor Cyan
    & git push origin $tag
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "git push origin $tag falhou; confira rede ou upstream. O release no GitHub pode ja existir."
    }

    Write-Host ""
    Write-Host "Release $tag publicado." -ForegroundColor Green
}
finally {
    Pop-Location
}
