#Requires -Version 5.1
<#
.SYNOPSIS
  Empacota um app do monorepo Ribanense Solucoes em zip + sha256 + copia do app.json.

.DESCRIPTION
  Publica (dotnet publish) o projeto src/aplicativos/Ribanense.Solucoes.App.<App>
  em Release win-x64 --no-self-contained, gera zip nomeado com a versao e o
  hash SHA256 ao lado, prontos para serem anexados a um GitHub Release.

.PARAMETER App
  Nome curto do app (case-sensitive), equivalente ao sufixo do projeto.
  Exemplo: "Winget" -> src\aplicativos\Ribanense.Solucoes.App.Winget\

.PARAMETER Version
  Versao SemVer a aplicar no nome do zip e no manifesto. Se omitida, tenta
  ler da Version do .csproj do app.

.PARAMETER OutputDir
  Pasta de saida (default: artifacts\publish\<App>).

.EXAMPLE
  .\publish-module.ps1 -App Winget -Version 1.0.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $App,
    [string] $Version,
    [string] $OutputDir
)

$ErrorActionPreference = 'Stop'
$ScriptRoot = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$ProjectRoot = Split-Path -Parent $ScriptRoot

$appProjectName = "Ribanense.Solucoes.App.$App"
$appProjectDir = Join-Path $ProjectRoot "src\aplicativos\$appProjectName"
$appProjectPath = Join-Path $appProjectDir "$appProjectName.csproj"

if (-not (Test-Path -LiteralPath $appProjectPath)) {
    throw "Projeto do app nao encontrado: $appProjectPath"
}

if (-not $Version) {
    [xml] $csproj = Get-Content -LiteralPath $appProjectPath -Raw
    $versionNode = $csproj.SelectSingleNode('//PropertyGroup/Version')
    if ($versionNode) { $Version = $versionNode.InnerText.Trim() }
    if (-not $Version) { $Version = '0.1.0' }
}

if (-not $OutputDir) {
    $OutputDir = Join-Path $ProjectRoot "artifacts\publish\$App"
}

if (Test-Path -LiteralPath $OutputDir) {
    Remove-Item -LiteralPath $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$publishDir = Join-Path $OutputDir 'out'
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

Write-Host "Publicando $appProjectName $Version..." -ForegroundColor Cyan
& dotnet publish $appProjectPath `
    -c Release `
    -r win-x64 `
    --no-self-contained `
    -p:PublishReadyToRun=true `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falhou." }

# Apps com py\<nome>.spec ganham um .exe Python standalone (sem Python no PC de destino).
$pySpec = Get-ChildItem -LiteralPath (Join-Path $appProjectDir 'py') -Filter '*.spec' -File -ErrorAction SilentlyContinue | Select-Object -First 1
if ($pySpec) {
    Write-Host "Gerando executavel Python standalone (PyInstaller)..." -ForegroundColor Cyan
    $exesAntes = @(Get-ChildItem -LiteralPath $publishDir -Filter '*.exe' -File | ForEach-Object Name)
    & python -m PyInstaller $pySpec.FullName --distpath $publishDir --workpath (Join-Path $OutputDir 'pybuild') --noconfirm
    if ($LASTEXITCODE -ne 0) {
        throw "PyInstaller falhou. Instale com: python -m pip install pyinstaller"
    }
    $exesNovos = @(Get-ChildItem -LiteralPath $publishDir -Filter '*.exe' -File | ForEach-Object Name | Where-Object { $_ -notin $exesAntes })
    if ($exesNovos.Count -ne 1) {
        throw "PyInstaller deveria gerar exatamente um .exe em $publishDir (gerou: $($exesNovos -join ', '))."
    }
    Write-Host "  Standalone: $($exesNovos[0])" -ForegroundColor DarkGray
}

$lowerApp = $App.ToLowerInvariant()
$zipName = "$lowerApp-$Version-win-x64.zip"
$zipPath = Join-Path $OutputDir $zipName
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }

Write-Host "Gerando zip: $zipName" -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath -Force

$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$shaPath = "$zipPath.sha256"
"$hash  $zipName" | Set-Content -LiteralPath $shaPath -Encoding ASCII

$manifestSrc = Join-Path $appProjectDir 'app.json'
$manifestDst = Join-Path $OutputDir 'app.json'
if (Test-Path -LiteralPath $manifestSrc) {
    Copy-Item -LiteralPath $manifestSrc -Destination $manifestDst -Force
}

Write-Host ""
Write-Host "Pacote criado em: $OutputDir" -ForegroundColor Green
Write-Host "  Zip    : $zipName"
Write-Host "  SHA256 : $hash"
if (Test-Path -LiteralPath $manifestDst) {
    Write-Host "  app.json: copiado"
}
