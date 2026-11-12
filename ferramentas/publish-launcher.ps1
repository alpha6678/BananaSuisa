#Requires -Version 5.1
<#
.SYNOPSIS
  Empacota o Launcher em .exe unico, pasta com manifesto e delta.

.DESCRIPTION
  Gera tres saidas em artifacts\publish\Launcher:

    1. launcher-<versao>-win-x64.exe  (+ .sha256)
       Self-contained + PublishSingleFile. E o arquivo que quem nao instalou
       baixa, como sempre foi. Tambem consegue se instalar sozinho, extraindo
       o conteudo de dentro dele (--install).

    2. launcher-<versao>-win-x64.zip  (+ .sha256) e payload-manifest.json
       Publicacao em pasta. E o formato da copia instalada, que atualiza por
       delta (delta-from-<anterior>.zip) quando a versao anterior tambem era
       pasta.

.PARAMETER Version
  Versao SemVer. Se omitida, usa //PropertyGroup/Version em Directory.Build.props.

.PARAMETER OutputDir
  Pasta de saida (default: artifacts\publish\Launcher).

.PARAMETER PreviousDir
  Pasta ja extraida da versao anterior, usada para calcular o delta.

.PARAMETER FromVersion
  Versao da pasta anterior. Obrigatoria junto com -PreviousDir.

.EXAMPLE
  .\publish-launcher.ps1 -Version 1.0.0
#>
[CmdletBinding()]
param(
    [string] $Version,
    [string] $OutputDir,
    [string] $PreviousDir,
    [string] $FromVersion
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

$folderDir = Join-Path $OutputDir 'out'
$singleFileDir = Join-Path $OutputDir 'out-single'
New-Item -ItemType Directory -Path $folderDir, $singleFileDir -Force | Out-Null

# 1. Pasta self-contained: formato da copia instalada.
Write-Host "Publicando Ribanense.Solucoes.Launcher $Version (pasta)..." -ForegroundColor Cyan
& dotnet publish $launcherProjectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishReadyToRun=true `
    "-p:Version=$Version" `
    "-p:AssemblyVersion=$(($Version -split '-')[0]).0" `
    "-p:FileVersion=$(($Version -split '-')[0]).0" `
    -o $folderDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish (pasta) do Launcher falhou." }

$publishedExe = Join-Path $folderDir 'Ribanense.Solucoes.Launcher.exe'
if (-not (Test-Path -LiteralPath $publishedExe)) {
    throw "Executavel nao encontrado: $publishedExe"
}

& (Join-Path $ScriptRoot 'payload-delta.ps1') `
    -TargetDir $folderDir `
    -Version $Version `
    -EntryExecutable 'Ribanense.Solucoes.Launcher.exe' `
    -PreviousDir $PreviousDir `
    -FromVersion $FromVersion `
    -OutputDir $OutputDir
if ($LASTEXITCODE -ne 0) { throw "payload-delta falhou." }

$zipName = "launcher-$Version-win-x64.zip"
$zipPath = Join-Path $OutputDir $zipName
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }

Write-Host "Gerando zip: $zipName" -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $folderDir '*') -DestinationPath $zipPath -Force

$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$zipHash  $zipName" | Set-Content -LiteralPath "$zipPath.sha256" -Encoding ASCII

# 2. Arquivo unico: o download de quem nao instalou.
#    E o bootstrap, com a pasta do launcher embutida como payload.zip. Rodar e
#    instalar extraem dessa copia local, sem baixar nada de novo.
$bootstrapProject = Join-Path $ProjectRoot 'src\Ribanense.Solucoes.LauncherBootstrap\Ribanense.Solucoes.LauncherBootstrap.csproj'
if (-not (Test-Path -LiteralPath $bootstrapProject)) {
    throw "Projeto do bootstrap nao encontrado: $bootstrapProject"
}

$bundleZip = Join-Path $OutputDir '_payload.zip'
if (Test-Path -LiteralPath $bundleZip) { Remove-Item -LiteralPath $bundleZip -Force }
Compress-Archive -Path (Join-Path $folderDir '*') -DestinationPath $bundleZip -Force

Write-Host "Publicando o exe unico (bootstrap + payload)..." -ForegroundColor Cyan
& dotnet publish $bootstrapProject `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeAllContentForSelfExtract=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    "-p:PayloadZip=$bundleZip" `
    "-p:Version=$Version" `
    "-p:AssemblyVersion=$(($Version -split '-')[0]).0" `
    -o $singleFileDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish do exe unico falhou." }
Remove-Item -LiteralPath $bundleZip -Force -ErrorAction SilentlyContinue

$singleExe = Join-Path $singleFileDir 'Ribanense.Solucoes.LauncherBootstrap.exe'
if (-not (Test-Path -LiteralPath $singleExe)) {
    throw "Executavel unico nao encontrado: $singleExe"
}

$exeName = "launcher-$Version-win-x64.exe"
$exePath = Join-Path $OutputDir $exeName
Copy-Item -LiteralPath $singleExe -Destination $exePath -Force

$exeHash = (Get-FileHash -LiteralPath $exePath -Algorithm SHA256).Hash.ToLowerInvariant()
"$exeHash  $exeName" | Set-Content -LiteralPath "$exePath.sha256" -Encoding ASCII

Write-Host ""
Write-Host "Pacote do Launcher criado em: $OutputDir" -ForegroundColor Green
Write-Host "  Exe    : $exeName ($([math]::Round((Get-Item $exePath).Length / 1MB, 1)) MB)"
Write-Host "  Zip    : $zipName"
Write-Host "  SHA256 : exe $exeHash"
Write-Host "           zip $zipHash"
