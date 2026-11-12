#Requires -Version 5.1
<#
.SYNOPSIS
  Gera o setup .exe do Launcher: baixa e instala a versao mais recente.

.DESCRIPTION
  O setup e um executavel pequeno, self-contained, que nao carrega o launcher.
  Ao rodar, ele:

    1. consulta a release launcher-v* mais recente no GitHub;
    2. baixa launcher-<versao>-win-x64.exe e o .sha256;
    3. confere o hash;
    4. executa o exe unico com --install --from <pasta>.

  Assim o instalador nunca fica velho: quem instala hoje recebe a versao
  publicada hoje.

.PARAMETER Version
  Versao do proprio setup (nao do launcher). Se omitida, usa a de Directory.Build.props.

.PARAMETER OutputDir
  Pasta de saida (default: artifacts\publish\Launcher).

.EXAMPLE
  .\publish-launcher-setup.ps1 -Version 0.2.0
#>
[CmdletBinding()]
param(
    [string] $Version,
    [string] $OutputDir
)

$ErrorActionPreference = 'Stop'
$ScriptRoot = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$ProjectRoot = Split-Path -Parent $ScriptRoot

if (-not $Version) {
    [xml] $xml = Get-Content -LiteralPath (Join-Path $ProjectRoot 'Directory.Build.props') -Raw
    $node = $xml.SelectSingleNode('//PropertyGroup/Version')
    if ($node) { $Version = $node.InnerText.Trim() }
    if (-not $Version) { throw "Versao do setup nao informada e ausente em Directory.Build.props." }
}

if (-not $OutputDir) {
    $OutputDir = Join-Path $ProjectRoot 'artifacts\publish\Launcher'
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$setupProject = Join-Path $ProjectRoot 'src\Ribanense.Solucoes.LauncherSetup\Ribanense.Solucoes.LauncherSetup.csproj'
if (-not (Test-Path -LiteralPath $setupProject)) {
    throw "Projeto do setup nao encontrado: $setupProject"
}

$setupOut = Join-Path $OutputDir 'out-setup'
if (Test-Path -LiteralPath $setupOut) { Remove-Item -LiteralPath $setupOut -Recurse -Force }

Write-Host "Publicando o setup $Version..." -ForegroundColor Cyan
& dotnet publish $setupProject `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeAllContentForSelfExtract=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    "-p:Version=$Version" `
    "-p:AssemblyVersion=$(($Version -split '-')[0]).0" `
    -o $setupOut
if ($LASTEXITCODE -ne 0) { throw "dotnet publish do setup falhou." }

$setupExe = Join-Path $setupOut 'Ribanense.Solucoes.LauncherSetup.exe'
if (-not (Test-Path -LiteralPath $setupExe)) {
    throw "Executavel do setup nao encontrado: $setupExe"
}

    $destName = "RibanenseSolucoesSetup-$Version.exe"
$destPath = Join-Path $OutputDir $destName
Copy-Item -LiteralPath $setupExe -Destination $destPath -Force

$hash = (Get-FileHash -LiteralPath $destPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $destName" | Set-Content -LiteralPath "$destPath.sha256" -Encoding ASCII

Write-Host ""
Write-Host "Setup criado: $destPath ($([math]::Round((Get-Item $destPath).Length / 1MB, 1)) MB)" -ForegroundColor Green
Write-Host "  SHA256 : $hash"
