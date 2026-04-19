#Requires -Version 5.1
# Ribanense.cli.ps1 — CLI de desenvolvimento do monorepo Ribanense Soluções.

param(
    [Parameter(Position = 0)]
    [string] $Command = 'help',

    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Rest
)

$ErrorActionPreference = 'Stop'

# ---------- Ambiente ----------

$script:CliRoot = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$script:ProjectRoot = Split-Path -Parent $script:CliRoot
$script:SolutionPath = Join-Path $script:ProjectRoot 'Ribanense.Solucoes.slnx'
$script:LauncherProjectPath = Join-Path $script:ProjectRoot 'src\Ribanense.Solucoes.Launcher\Ribanense.Solucoes.Launcher.csproj'
$script:AppsRoot = Join-Path $script:ProjectRoot 'src\aplicativos'
$script:LauncherDataRoot = Join-Path $env:LOCALAPPDATA 'Ribanense Soluções'
$script:DevLinkRoot = Join-Path $script:LauncherDataRoot 'aplicativos'

$script:RestArguments = @(
    foreach ($Argument in $Rest) {
        if (-not [string]::IsNullOrWhiteSpace($Argument)) {
            $Argument
        }
    }
)

# ---------- Saida colorida ----------

function Write-Ok    { param([string] $m) Write-Host "[OK] $m"  -ForegroundColor Green }
function Write-Err   { param([string] $m) Write-Host "[ERR] $m" -ForegroundColor Red }
function Write-Info  { param([string] $m) Write-Host "[..] $m"  -ForegroundColor Cyan }
function Write-Warn2 { param([string] $m) Write-Host "[!!] $m"  -ForegroundColor Yellow }
function Write-Muted { param([string] $m) Write-Host "     $m"  -ForegroundColor DarkGray }
function Write-Step  { param([string] $m) Write-Host ""; Write-Host "=== $m ===" -ForegroundColor Magenta }

# ---------- Helpers basicos ----------

function Assert-PathExists {
    param([Parameter(Mandatory)] [string] $Path, [Parameter(Mandatory)] [string] $Description)
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Description nao encontrado: $Path"
    }
}

function Assert-CommandAvailable {
    param([Parameter(Mandatory)] [string] $CommandName)
    if (-not (Get-Command $CommandName -ErrorAction SilentlyContinue)) {
        throw "Comando nao encontrado no PATH: $CommandName"
    }
}

function Get-AppProjectPath {
    param([Parameter(Mandatory)] [string] $AppName)
    $projName = "Ribanense.Solucoes.App.$AppName"
    $projPath = Join-Path (Join-Path $script:AppsRoot $projName) "$projName.csproj"
    if (-not (Test-Path -LiteralPath $projPath)) {
        throw "App '$AppName' nao encontrado em src\aplicativos\. Use 'rb list' para ver apps disponiveis."
    }
    return $projPath
}

function Get-AppOutputExe {
    param([Parameter(Mandatory)] [string] $AppName)
    $projName = "Ribanense.Solucoes.App.$AppName"
    return Join-Path $script:AppsRoot "$projName\bin\Debug\net10.0-windows\$projName.exe"
}

function Get-AppOutputDir {
    param([Parameter(Mandatory)] [string] $AppName)
    $projName = "Ribanense.Solucoes.App.$AppName"
    return Join-Path $script:AppsRoot "$projName\bin\Debug\net10.0-windows"
}

function Get-RibananseProcessesFromRepo {
    param([string[]] $NameFilters = @('Ribanense.Solucoes.Launcher', 'Ribanense.Solucoes.App.*'))
    $root = [System.IO.Path]::GetFullPath($script:ProjectRoot)
    $list = @()
    foreach ($filter in $NameFilters) {
        foreach ($p in (Get-Process -Name $filter -ErrorAction SilentlyContinue)) {
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
    }
    return $list
}

function Stop-RibananseProcesses {
    param([string[]] $NameFilters = @('Ribanense.Solucoes.Launcher', 'Ribanense.Solucoes.App.*'))
    $procs = Get-RibananseProcessesFromRepo -NameFilters $NameFilters
    if ($procs.Count -eq 0) { return }
    foreach ($p in $procs) {
        Write-Warn2 "Encerrando instancia (PID $($p.Id), $($p.ProcessName))..."
        try { Stop-Process -Id $p.Id -Force -ErrorAction Stop }
        catch { Write-Warn2 "Nao foi possivel encerrar PID $($p.Id) (pode estar elevado)." }
    }
    Start-Sleep -Milliseconds 400
}

function Invoke-DotNet {
    param([Parameter(Mandatory)] [string[]] $Arguments)
    Assert-CommandAvailable 'dotnet'
    Push-Location $script:ProjectRoot
    try {
        & dotnet @Arguments
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    finally { Pop-Location }
}

function Get-AllAppProjects {
    if (-not (Test-Path -LiteralPath $script:AppsRoot)) { return @() }
    $results = @()
    foreach ($dir in Get-ChildItem -LiteralPath $script:AppsRoot -Directory -ErrorAction SilentlyContinue) {
        $projName = $dir.Name
        if ($projName -notlike 'Ribanense.Solucoes.App.*') { continue }
        $csproj = Join-Path $dir.FullName "$projName.csproj"
        if (-not (Test-Path -LiteralPath $csproj)) { continue }

        $shortName = $projName.Substring('Ribanense.Solucoes.App.'.Length)
        $version = $null
        try {
            [xml] $xml = Get-Content -LiteralPath $csproj -Raw
            $verNode = $xml.SelectSingleNode('//PropertyGroup/Version')
            if ($verNode) { $version = $verNode.InnerText.Trim() }
        } catch { }

        $manifestPath = Join-Path $dir.FullName 'app.json'
        $manifestVersion = $null
        $publicName = $null
        if (Test-Path -LiteralPath $manifestPath) {
            try {
                $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -ErrorAction SilentlyContinue
                if ($manifest) {
                    $manifestVersion = $manifest.version
                    $publicName = if ($manifest.publicName) { $manifest.publicName } else { $manifest.name }
                }
            } catch { }
        }

        $results += [pscustomobject]@{
            ShortName       = $shortName
            ProjectName     = $projName
            ProjectPath     = $csproj
            CsprojVersion   = $version
            ManifestVersion = $manifestVersion
            PublicName      = $publicName
            HasManifest     = (Test-Path -LiteralPath $manifestPath)
            Directory       = $dir.FullName
        }
    }
    return $results
}

function Get-SdkVersion {
    $sdkFile = Join-Path $script:ProjectRoot 'src\Ribanense.Solucoes.PluginSDK\SdkVersion.cs'
    if (-not (Test-Path -LiteralPath $sdkFile)) { return '0.0.0' }
    $content = Get-Content -LiteralPath $sdkFile -Raw
    if ($content -match '"(\d+\.\d+\.\d+(?:-[\w\.-]+)?)"') {
        return $Matches[1]
    }
    return '0.0.0'
}

function Get-LauncherVersion {
    $buildProps = Join-Path $script:ProjectRoot 'Directory.Build.props'
    if (Test-Path -LiteralPath $buildProps) {
        try {
            [xml] $xml = Get-Content -LiteralPath $buildProps -Raw
            $node = $xml.SelectSingleNode('//PropertyGroup/Version')
            if ($node) { return $node.InnerText.Trim() }
        } catch { }
    }
    return '0.0.0'
}

# ---------- Comandos ----------

function Invoke-SolutionBuild {
    Assert-PathExists -Path $script:SolutionPath -Description 'Solution'
    Stop-RibananseProcesses
    Write-Info "dotnet build $($script:SolutionPath | Split-Path -Leaf)"
    Invoke-DotNet -Arguments (@('build', $script:SolutionPath) + $script:RestArguments)
    Write-Ok "Build concluido."
}

function Invoke-AppRun {
    if ($script:RestArguments.Count -lt 1) {
        Invoke-LauncherRun
        return
    }
    $appName = $script:RestArguments[0]
    $projPath = Get-AppProjectPath -AppName $appName
    Stop-RibananseProcesses -NameFilters @("Ribanense.Solucoes.App.$appName")
    Write-Info "Compilando app '$appName'..."
    Invoke-DotNet -Arguments @('build', $projPath)
    $exePath = Get-AppOutputExe -AppName $appName
    Assert-PathExists -Path $exePath -Description "Executavel do app '$appName'"
    Write-Ok "Abrindo '$appName'..."
    Start-Process -FilePath $exePath
}

function Invoke-LauncherRun {
    Assert-PathExists -Path $script:LauncherProjectPath -Description 'Projeto do Launcher'
    Stop-RibananseProcesses -NameFilters @('Ribanense.Solucoes.Launcher')
    Write-Info "Compilando Launcher..."
    Invoke-DotNet -Arguments (@('build', $script:LauncherProjectPath) + $script:RestArguments)
    $exePath = Join-Path $script:ProjectRoot 'src\Ribanense.Solucoes.Launcher\bin\Debug\net10.0-windows\Ribanense.Solucoes.Launcher.exe'
    Assert-PathExists -Path $exePath -Description 'Executavel do Launcher'
    Write-Ok "Abrindo Launcher..."
    Start-Process -FilePath $exePath
}

function Invoke-SolutionTests {
    Assert-PathExists -Path $script:SolutionPath -Description 'Solution'
    Write-Info "dotnet test"
    Invoke-DotNet -Arguments (@('test', $script:SolutionPath) + $script:RestArguments)
    Write-Ok "Testes concluidos."
}

function Invoke-FullCheck {
    Stop-RibananseProcesses
    Write-Step "Build"
    Invoke-DotNet -Arguments @('build', $script:SolutionPath)
    Write-Step "Testes"
    Invoke-DotNet -Arguments @('test', $script:SolutionPath)
    Write-Ok "Check completo."
}

function Invoke-Clean {
    Stop-RibananseProcesses
    $patterns = @('bin', 'obj')
    $removed = 0
    $freed = 0L

    foreach ($pattern in $patterns) {
        foreach ($d in (Get-ChildItem -LiteralPath $script:ProjectRoot -Directory -Recurse -Filter $pattern -Force -ErrorAction SilentlyContinue)) {
            try {
                $size = (Get-ChildItem -LiteralPath $d.FullName -Recurse -File -Force -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
                if ($size) { $freed += $size }
                Remove-Item -LiteralPath $d.FullName -Recurse -Force -ErrorAction Stop
                $removed++
            } catch {
                Write-Warn2 "Nao foi possivel remover: $($d.FullName) ($($_.Exception.Message))"
            }
        }
    }

    $artifacts = Join-Path $script:ProjectRoot 'artifacts'
    if (Test-Path -LiteralPath $artifacts) {
        try {
            $size = (Get-ChildItem -LiteralPath $artifacts -Recurse -File -Force -ErrorAction SilentlyContinue | Measure-Object Length -Sum).Sum
            if ($size) { $freed += $size }
            Remove-Item -LiteralPath $artifacts -Recurse -Force
            $removed++
        } catch {
            Write-Warn2 "Nao foi possivel remover: $artifacts"
        }
    }

    $mb = [math]::Round($freed / 1MB, 1)
    Write-Ok "Limpeza concluida: $removed diretorio(s) removido(s), ~$mb MB liberados."
}

function Invoke-ListApps {
    $apps = Get-AllAppProjects
    if ($apps.Count -eq 0) {
        Write-Warn2 "Nenhum app encontrado em src\aplicativos\."
        return
    }
    Write-Host ""
    Write-Host ("{0,-14} {1,-10} {2,-10} {3}" -f 'ShortName', 'csproj', 'app.json', 'PublicName') -ForegroundColor Gray
    Write-Host ("{0,-14} {1,-10} {2,-10} {3}" -f '---------', '------', '--------', '----------') -ForegroundColor DarkGray
    foreach ($a in $apps) {
        $csprojVer = if ($a.CsprojVersion) { $a.CsprojVersion } else { '?' }
        $manifestVer = if ($a.ManifestVersion) { $a.ManifestVersion } else { '-' }
        $pub = if ($a.PublicName) { $a.PublicName } else { '(sem app.json)' }
        Write-Host ("{0,-14} {1,-10} {2,-10} {3}" -f $a.ShortName, $csprojVer, $manifestVer, $pub)
    }
    Write-Host ""
    Write-Muted "Total: $($apps.Count) app(s)."
}

function Invoke-ShowVersions {
    $launcher = Get-LauncherVersion
    $sdk = Get-SdkVersion
    Write-Host ""
    Write-Host ("{0,-30} {1}" -f 'Launcher (Directory.Build.props)', $launcher) -ForegroundColor Cyan
    Write-Host ("{0,-30} {1}" -f 'PluginSDK.SdkVersion.Current', $sdk) -ForegroundColor Cyan

    $apps = Get-AllAppProjects
    if ($apps.Count -gt 0) {
        Write-Host ""
        Write-Host "Apps:" -ForegroundColor Cyan
        foreach ($a in $apps) {
            $csprojVer = if ($a.CsprojVersion) { $a.CsprojVersion } else { '?' }
            $manifestVer = if ($a.ManifestVersion) { $a.ManifestVersion } else { '?' }
            $match = if ($csprojVer -eq $manifestVer) { '' } else { ' <!> csproj vs app.json divergente' }
            $line = "  {0,-14} csproj={1,-8} manifesto={2,-8}{3}" -f $a.ShortName, $csprojVer, $manifestVer, $match
            if ($match) {
                Write-Host $line -ForegroundColor Yellow
            } else {
                Write-Host $line
            }
        }
    }
    Write-Host ""
}

function Invoke-DevLink {
    if ($script:RestArguments.Count -lt 1) {
        throw "Uso: rb devlink <App>. Exemplo: rb devlink Winget"
    }
    $appName = $script:RestArguments[0]
    $projPath = Get-AppProjectPath -AppName $appName
    $outDir = Get-AppOutputDir -AppName $appName
    $manifestPath = Join-Path (Split-Path -Parent $projPath) 'app.json'
    $dest = Join-Path $script:DevLinkRoot $appName

    Stop-RibananseProcesses -NameFilters @("Ribanense.Solucoes.App.$appName")

    Write-Info "Compilando '$appName'..."
    Invoke-DotNet -Arguments @('build', $projPath)

    if (-not (Test-Path -LiteralPath $outDir)) {
        throw "Diretorio de build nao encontrado: $outDir"
    }

    if (Test-Path -LiteralPath $dest) {
        Write-Muted "Substituindo devlink existente em $dest"
        Remove-Item -LiteralPath $dest -Recurse -Force
    }
    New-Item -ItemType Directory -Path $dest -Force | Out-Null

    Write-Info "Copiando binarios para $dest"
    Copy-Item -Path (Join-Path $outDir '*') -Destination $dest -Recurse -Force

    if (Test-Path -LiteralPath $manifestPath) {
        Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $dest 'app.json') -Force
        Write-Muted "app.json copiado."
    } else {
        Write-Warn2 "app.json nao encontrado em $manifestPath - Launcher nao vai reconhecer o app sem ele."
    }

    Write-Ok "Devlink concluido: '$appName' disponivel no Launcher."
    Write-Muted "Pasta: $dest"
    Write-Muted "Abra/reinicie o Launcher com 'rb run'."
}

function Invoke-DevUnlink {
    if ($script:RestArguments.Count -lt 1) {
        throw "Uso: rb unlink <App>. Exemplo: rb unlink Winget"
    }
    $appName = $script:RestArguments[0]
    $dest = Join-Path $script:DevLinkRoot $appName

    if (-not (Test-Path -LiteralPath $dest)) {
        Write-Warn2 "Nada para remover: $dest nao existe."
        return
    }

    Stop-RibananseProcesses -NameFilters @("Ribanense.Solucoes.App.$appName")
    Remove-Item -LiteralPath $dest -Recurse -Force
    Write-Ok "Devlink de '$appName' removido."
}

function Invoke-AppPublish {
    if ($script:RestArguments.Count -lt 1) {
        throw "Uso: rb publish <App> [-Version <ver>]. Exemplo: rb publish Winget -Version 0.1.0"
    }
    $appName = $script:RestArguments[0]
    # Garante que o app existe antes de chamar o script
    Get-AppProjectPath -AppName $appName | Out-Null

    $publishScript = Join-Path $script:CliRoot 'publish-module.ps1'
    Assert-PathExists -Path $publishScript -Description 'publish-module.ps1'
    $remaining = @()
    if ($script:RestArguments.Count -gt 1) {
        $remaining = $script:RestArguments[1..($script:RestArguments.Count - 1)]
    }
    & $publishScript -App $appName @remaining
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Ok "Pacote de '$appName' gerado em artifacts\publish\$appName\."
}

function Invoke-AppRelease {
    if ($script:RestArguments.Count -lt 2) {
        throw "Uso: rb release <App> <semver>. Exemplo: rb release Winget 0.1.0"
    }
    $appName = $script:RestArguments[0]
    $version = $script:RestArguments[1]
    Get-AppProjectPath -AppName $appName | Out-Null

    $releaseScript = Join-Path $script:CliRoot 'release.ps1'
    Assert-PathExists -Path $releaseScript -Description 'release.ps1'
    & $releaseScript -App $appName -Version $version
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Ok "Release '$appName' $version publicado."
}

# ---------- Tabela de comandos ----------

$script:Commands = @(
    [pscustomobject]@{ Verb = 'build';   Aliases = @('compilar');          Handler = 'Invoke-SolutionBuild'; Usage = 'rb build [args]';                    Help = 'Compila a solution inteira.' },
    [pscustomobject]@{ Verb = 'run';     Aliases = @('rodar');             Handler = 'Invoke-AppRun';        Usage = 'rb run [App]';                       Help = 'Compila e abre o Launcher (ou o app informado). Ex: rb run Winget' },
    [pscustomobject]@{ Verb = 'test';    Aliases = @('testar');            Handler = 'Invoke-SolutionTests'; Usage = 'rb test [args]';                     Help = 'Executa dotnet test.' },
    [pscustomobject]@{ Verb = 'check';   Aliases = @('validar');           Handler = 'Invoke-FullCheck';     Usage = 'rb check';                           Help = 'Build + test em sequencia.' },
    [pscustomobject]@{ Verb = 'clean';   Aliases = @('limpar');            Handler = 'Invoke-Clean';         Usage = 'rb clean';                           Help = 'Remove bin/, obj/, artifacts/ e encerra processos Ribanense do repo.' },
    [pscustomobject]@{ Verb = 'list';    Aliases = @('apps', 'ls');        Handler = 'Invoke-ListApps';      Usage = 'rb list';                            Help = 'Lista apps disponiveis em src\aplicativos\.' },
    [pscustomobject]@{ Verb = 'version'; Aliases = @('versao');            Handler = 'Invoke-ShowVersions';  Usage = 'rb version';                         Help = 'Mostra versoes de Launcher, SDK e cada app (alerta se csproj e app.json divergem).' },
    [pscustomobject]@{ Verb = 'devlink'; Aliases = @('link');              Handler = 'Invoke-DevLink';       Usage = 'rb devlink <App>';                   Help = 'Compila um app e copia para %LOCALAPPDATA%\Ribanense Solucoes\aplicativos\ para o Launcher ver como instalado.' },
    [pscustomobject]@{ Verb = 'unlink';  Aliases = @('devunlink');         Handler = 'Invoke-DevUnlink';     Usage = 'rb unlink <App>';                    Help = 'Remove o devlink de um app.' },
    [pscustomobject]@{ Verb = 'publish'; Aliases = @('empacotar');         Handler = 'Invoke-AppPublish';    Usage = 'rb publish <App> [-Version <ver>]';  Help = 'Empacota um app em zip + sha256 + copia do app.json.' },
    [pscustomobject]@{ Verb = 'release'; Aliases = @();                    Handler = 'Invoke-AppRelease';    Usage = 'rb release <App> <semver>';          Help = 'Cria tag git e publica GitHub Release (requer gh CLI).' },
    [pscustomobject]@{ Verb = 'help';    Aliases = @('?', '-h', '--help'); Handler = 'Show-RibananseCliHelp'; Usage = 'rb help';                           Help = 'Esta ajuda.' }
)

function Show-RibananseCliHelp {
    Write-Host ""
    Write-Host "Ribanense Solucoes — CLI de desenvolvimento" -ForegroundColor Cyan
    Write-Host "============================================" -ForegroundColor DarkCyan
    Write-Host ""
    Write-Host "Comandos:" -ForegroundColor Gray
    foreach ($c in $script:Commands) {
        $aliasStr = if ($c.Aliases.Count -gt 0) { " ({0})" -f ($c.Aliases -join ', ') } else { '' }
        $verbLine = "  {0}{1}" -f $c.Verb, $aliasStr
        Write-Host $verbLine -ForegroundColor Yellow
        Write-Host ("      Uso    : {0}" -f $c.Usage) -ForegroundColor Gray
        Write-Host ("      Descr. : {0}" -f $c.Help) -ForegroundColor DarkGray
    }
    Write-Host ""
    Write-Host "Exemplos:" -ForegroundColor Gray
    Write-Host "  .\rb.cmd list"
    Write-Host "  .\rb.cmd run"
    Write-Host "  .\rb.cmd run Winget"
    Write-Host "  .\rb.cmd devlink Winget"
    Write-Host "  .\rb.cmd check"
    Write-Host "  .\rb.cmd publish Winget -Version 0.1.0"
    Write-Host "  .\rb.cmd release Winget 0.1.0"
    Write-Host ""
}

function Find-MatchingCommand {
    param([Parameter(Mandatory)] [string] $VerbOrAlias)
    $q = $VerbOrAlias.ToLowerInvariant()
    foreach ($c in $script:Commands) {
        if ($c.Verb -eq $q) { return $c }
        foreach ($a in $c.Aliases) {
            if ($a -eq $q) { return $c }
        }
    }
    return $null
}

function Show-CommandSuggestions {
    param([Parameter(Mandatory)] [string] $VerbOrAlias)
    $q = $VerbOrAlias.ToLowerInvariant()
    $candidates = @()
    foreach ($c in $script:Commands) {
        $all = @($c.Verb) + $c.Aliases
        foreach ($name in $all) {
            if ($name.StartsWith($q) -or $q.StartsWith($name) -or $name.Contains($q)) {
                $candidates += $c.Verb
                break
            }
        }
    }
    $candidates = $candidates | Select-Object -Unique
    if ($candidates.Count -gt 0 -and $candidates.Count -lt $script:Commands.Count) {
        Write-Host ""
        Write-Muted "Quis dizer: $($candidates -join ', ')?"
    }
}

# ---------- Dispatch ----------

try {
    $match = Find-MatchingCommand -VerbOrAlias $Command
    if ($null -eq $match) {
        Write-Err "Comando desconhecido: '$Command'"
        Show-CommandSuggestions -VerbOrAlias $Command
        Write-Host ""
        Show-RibananseCliHelp
        exit 1
    }

    & $match.Handler
}
catch {
    Write-Host ""
    Write-Err $_.Exception.Message
    if ($env:RIBANENSE_CLI_TRACE -eq '1') {
        Write-Host ""
        Write-Muted ($_ | Out-String).Trim()
    } else {
        Write-Muted "Dica: defina RIBANENSE_CLI_TRACE=1 para ver o stack trace."
    }
    exit 1
}
