#Requires -Version 5.1
<#
.SYNOPSIS
  Empacota o Launcher em zip + SHA256 (sem app.json — o Launcher nao usa manifesto de plugin).

.DESCRIPTION
  Publica dotnet publish do projeto Ribanense.Solucoes.Launcher em Release win-x64
  --no-self-contained. Versao default lida de Directory.Build.props na raiz do repo.

.PARAMETER Version
  Versao SemVer no nome do zip. Se omitida, usa //PropertyGroup/Version em Directory.Build.props.

.PARAMETER OutputDir
  Pasta de saida (default: artifacts\publish\Launcher).

.EXAMPLE
  .\publish-launcher.ps1 -Version 1.0.0
#>
[CmdletBinding()]
param(
    [string] $Version,
    [string] $OutputDir
)

$ErrorActionPreference = 'Stop'
$ScriptRoot = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$ProjectRoot = Split-Path -Parent $ScriptRoot

$launcherProjectPath = Join-Path $ProjectRoot 'src\Ribanense.Solucoes.Launcher\Ribanense.Solucoes.Launcher.csproj'
if (-not (Test-Path -LiteralPath $launcherProjectPath)) {
    throw "Projeto do Launcher nao encontrado: $launcherProjectPath"
}

$buildProps = Join-Path $ProjectRoot 'Directory.Build.props'
if (-not $Version) {
    if (-not (Test-Path -LiteralPath $buildProps)) {
        throw "Directory.Build.props nao encontrado e -Version nao informado."
    }
    [xml] $xml = Get-Content -LiteralPath $buildProps -Raw
    $node = $xml.SelectSingleNode('//PropertyGroup/Version')
    if (-not $node) { throw "Nao foi possivel ler Version em Directory.Build.props." }
    $Version = $node.InnerText.Trim()
    if (-not $Version) { throw "Version vazia em Directory.Build.props." }
}

if (-not $OutputDir) {
    $OutputDir = Join-Path $ProjectRoot 'artifacts\publish\Launcher'
}

if (Test-Path -LiteralPath $OutputDir) {
    Remove-Item -LiteralPath $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$publishDir = Join-Path $OutputDir 'out'
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

Write-Host "Publicando Ribanense.Solucoes.Launcher $Version..." -ForegroundColor Cyan
& dotnet publish $launcherProjectPath `
    -c Release `
    -r win-x64 `
    --no-self-contained `
    -p:PublishReadyToRun=true `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish do Launcher falhou." }

$zipName = "launcher-$Version-win-x64.zip"
$zipPath = Join-Path $OutputDir $zipName
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }

Write-Host "Gerando zip: $zipName" -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -Force

$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$shaPath = "$zipPath.sha256"
"$hash  $zipName" | Set-Content -LiteralPath $shaPath -Encoding ASCII

Write-Host ""
Write-Host "Pacote do Launcher criado em: $OutputDir" -ForegroundColor Green
Write-Host "  Zip    : $zipName"
Write-Host "  SHA256 : $hash"
