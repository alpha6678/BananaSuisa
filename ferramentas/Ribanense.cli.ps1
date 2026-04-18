#Requires -Version 5.1
# Ribanense.cli.ps1 — CLI de desenvolvimento do monorepo Ribanense Soluções.

param(
    [Parameter(Position = 0)]
    [string] $Command = 'help',

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Rest
)

$ErrorActionPreference = 'Stop'

$script:CliRoot = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$script:ProjectRoot = Split-Path -Parent $script:CliRoot
$script:SolutionPath = Join-Path $script:ProjectRoot 'Ribanense.Solucoes.slnx'
$script:LauncherProjectPath = Join-Path $script:ProjectRoot 'src\Ribanense.Solucoes.Launcher\Ribanense.Solucoes.Launcher.csproj'
$script:AppsRoot = Join-Path $script:ProjectRoot 'src\aplicativos'
$script:RestArguments = @(
    foreach ($Argument in $Rest) {
        if (-not [string]::IsNullOrWhiteSpace($Argument)) {
            $Argument
        }
    }
)

function Assert-PathExists {
    param(
        [Parameter(Mandatory = $true)] [string] $Path,
        [Parameter(Mandatory = $true)] [string] $Description
    )
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Description nao encontrado: $Path"
    }
}

function Assert-CommandAvailable {
    param([Parameter(Mandatory = $true)] [string] $CommandName)
    if (-not (Get-Command $CommandName -ErrorAction SilentlyContinue)) {
        throw "Comando nao encontrado no PATH: $CommandName"
    }
}

function Assert-NoExtraArguments {
    param([Parameter(Mandatory = $true)] [string] $CommandName)
    if ($script:RestArguments.Count -gt 0) {
        throw "O comando '$CommandName' nao aceita argumentos extras: $($script:RestArguments -join ' ')"
    }
}

function Get-LauncherProcessesFromRepo {
    $root = [System.IO.Path]::GetFullPath($script:ProjectRoot)
    $list = @()
    foreach ($p in (Get-Process -Name 'Ribanense.Solucoes.Launcher' -ErrorAction SilentlyContinue)) {
        $exePath = $null
        try {
            $exePath = $p.Path
            if (-not $exePath -and $p.MainModule) { $exePath = $p.MainModule.FileName }
        } catch { }

        if ([string]::IsNullOrWhiteSpace($exePath)) {
            $list += $p
        } else {
            $exeFull = [System.IO.Path]::GetFullPath($exePath)
            if ($exeFull.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
                $list += $p
            }
        }
    }
    return $list
}

function Stop-LauncherIfRunning {
    $procs = Get-LauncherProcessesFromRepo
    if ($procs.Count -eq 0) { return }
    foreach ($p in $procs) {
        Write-Host "Encerrando instancia anterior do Launcher (PID $($p.Id))..." -ForegroundColor Yellow
        try { Stop-Process -Id $p.Id -Force -ErrorAction Stop }
        catch { Write-Warning "Nao foi possivel encerrar o processo $($p.Id) (pode estar elevado)." }
    }
    Start-Sleep -Milliseconds 400
}

function Invoke-DotNetCommand {
    param([Parameter(Mandatory = $true)] [string[]] $Arguments)
    Assert-CommandAvailable 'dotnet'
    Push-Location $script:ProjectRoot
    try {
        & dotnet @Arguments
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    finally { Pop-Location }
}

function Invoke-SolutionBuild {
    Assert-PathExists -Path $script:SolutionPath -Description 'Solution'
    Stop-LauncherIfRunning
    Invoke-DotNetCommand -Arguments (@('build', $script:SolutionPath) + $script:RestArguments)
}

function Invoke-LauncherRun {
    Assert-PathExists -Path $script:LauncherProjectPath -Description 'Projeto do Launcher'
    Stop-LauncherIfRunning
    Invoke-DotNetCommand -Arguments (@('build', $script:LauncherProjectPath) + $script:RestArguments)
    $exePath = Join-Path $script:ProjectRoot 'src\Ribanense.Solucoes.Launcher\bin\Debug\net10.0-windows\Ribanense.Solucoes.Launcher.exe'
    if (-not (Test-Path -LiteralPath $exePath)) {
        throw "Executavel nao encontrado apos o build: $exePath"
    }
    Write-Host "`nAbrindo Launcher..." -ForegroundColor Cyan
    Start-Process -FilePath $exePath
}

function Invoke-AppRun {
    if ($script:RestArguments.Count -lt 1) {
        Invoke-LauncherRun
        return
    }
    $appName = $script:RestArguments[0]
    $projName = "Ribanense.Solucoes.App.$appName"
    $projPath = Join-Path $script:AppsRoot "$projName\$projName.csproj"
    Assert-PathExists -Path $projPath -Description "Projeto do app '$appName'"
    Invoke-DotNetCommand -Arguments @('build', $projPath)
    $exePath = Join-Path (Split-Path -Parent $projPath) "bin\Debug\net10.0-windows\$projName.exe"
    if (-not (Test-Path -LiteralPath $exePath)) {
        throw "Executavel nao encontrado apos o build: $exePath"
    }
    Write-Host "`nAbrindo app '$appName'..." -ForegroundColor Cyan
    Start-Process -FilePath $exePath
}

function Invoke-SolutionTests {
    Assert-PathExists -Path $script:SolutionPath -Description 'Solution'
    Invoke-DotNetCommand -Arguments (@('test', $script:SolutionPath) + $script:RestArguments)
}

function Invoke-FullCheck {
    Assert-NoExtraArguments 'check'
    Stop-LauncherIfRunning
    Invoke-DotNetCommand -Arguments @('build', $script:SolutionPath)
    Invoke-DotNetCommand -Arguments @('test', $script:SolutionPath)
}

function Resolve-AppProject {
    param([Parameter(Mandatory = $true)] [string] $AppName)
    $projName = "Ribanense.Solucoes.App.$AppName"
    $projPath = Join-Path (Join-Path $script:AppsRoot $projName) "$projName.csproj"
    if (-not (Test-Path -LiteralPath $projPath)) {
        throw "App '$AppName' nao encontrado. Esperado em: $projPath"
    }
    return $projPath
}

function Invoke-AppPublish {
    if ($script:RestArguments.Count -lt 1) {
        throw "Uso: rb publish <nome-do-app> [-Version <semver>]. Exemplo: rb publish Winget -Version 1.0.0"
    }
    $appName = $script:RestArguments[0]
    $publishScript = Join-Path $script:CliRoot 'publish-module.ps1'
    Assert-PathExists -Path $publishScript -Description 'publish-module.ps1'
    $remaining = @()
    if ($script:RestArguments.Count -gt 1) {
        $remaining = $script:RestArguments[1..($script:RestArguments.Count - 1)]
    }
    & $publishScript -App $appName @remaining
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

function Invoke-AppRelease {
    if ($script:RestArguments.Count -lt 2) {
        throw "Uso: rb release <nome-do-app> <semver>. Exemplo: rb release Winget 1.0.0"
    }
    $appName = $script:RestArguments[0]
    $version = $script:RestArguments[1]
    $releaseScript = Join-Path $script:CliRoot 'release.ps1'
    Assert-PathExists -Path $releaseScript -Description 'release.ps1'
    & $releaseScript -App $appName -Version $version
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

function Show-RibananseCliHelp {
    Write-Host @'
Ribanense Solucoes — CLI de desenvolvimento

Comandos:
  build, compilar               dotnet build Ribanense.Solucoes.slnx
  run, rodar [App]              Compila e abre o Launcher (ou o app informado, ex: rb run Winget)
  test, testar                  dotnet test Ribanense.Solucoes.slnx
  check, validar                build + test
  publish <app> [-Version ...]  Empacota um app em zip + sha256 + app.json
  release <app> <semver>        Cria tag e publica no GitHub Releases (requer gh)
  help                          Esta ajuda

Exemplos:
  .\rb.cmd build
  .\rb.cmd run
  .\rb.cmd run Winget
  .\rb.cmd test
  .\rb.cmd check
  .\rb.cmd publish Winget -Version 0.1.0
  .\rb.cmd release Winget 0.1.0

'@
}

switch -Regex ($Command.ToLowerInvariant()) {
    '^(build|compilar)$'   { Invoke-SolutionBuild }
    '^(run|rodar)$'        { Invoke-AppRun }
    '^(test|testar)$'      { Invoke-SolutionTests }
    '^(check|validar)$'    { Invoke-FullCheck }
    '^(publish|empacotar)$' { Invoke-AppPublish }
    '^release$'            { Invoke-AppRelease }
    '^(help|\?|-h|--help)$' {
        Assert-NoExtraArguments 'help'
        Show-RibananseCliHelp
    }
    default {
        Write-Warning "Comando desconhecido: $Command"
        Show-RibananseCliHelp
        exit 1
    }
}
