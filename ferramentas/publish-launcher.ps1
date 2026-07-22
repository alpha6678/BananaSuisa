#Requires -Version 5.1
<#
.SYNOPSIS
  Empacota o Launcher em um unico .exe self-contained + SHA256 (sem app.json).

.DESCRIPTION
  Publica dotnet publish do projeto Ribanense.Solucoes.Launcher em Release win-x64
  self-contained + PublishSingleFile. O asset principal e um .exe executavel
  direto (sem extrair pasta). Versao default lida de Directory.Build.props.

.PARAMETER Version
  Versao SemVer no nome do .exe. Se omitida, usa //PropertyGroup/Version em Directory.Build.props.

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

Write-Host "Publicando Ribanense.Solucoes.Launcher $Version (single-file self-contained)..." -ForegroundColor Cyan
& dotnet publish $launcherProjectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishReadyToRun=true `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish do Launcher falhou." }

$publishedExe = Join-Path $publishDir 'Ribanense.Solucoes.Launcher.exe'
if (-not (Test-Path -LiteralPath $publishedExe)) {
    throw "Executavel single-file nao encontrado: $publishedExe"
}

$exeName = "launcher-$Version-win-x64.exe"
$exePath = Join-Path $OutputDir $exeName
Copy-Item -LiteralPath $publishedExe -Destination $exePath -Force

$hash = (Get-FileHash -LiteralPath $exePath -Algorithm SHA256).Hash.ToLowerInvariant()
$shaPath = "$exePath.sha256"
"$hash  $exeName" | Set-Content -LiteralPath $shaPath -Encoding ASCII

Write-Host ""
Write-Host "Pacote do Launcher criado em: $OutputDir" -ForegroundColor Green
Write-Host "  Exe    : $exeName"
Write-Host "  SHA256 : $hash"
