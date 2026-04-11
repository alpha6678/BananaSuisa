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
$script:SolutionPath = Join-Path $script:ProjectRoot 'BananaSuisa.slnx'
$script:AppProjectPath = Join-Path $script:ProjectRoot 'src\BananaSuisa.App\BananaSuisa.App.csproj'
$script:RestArguments = @(
    foreach ($Argument in $Rest) {
        if (-not [string]::IsNullOrWhiteSpace($Argument)) {
            $Argument
        }
    }
)

function Assert-PathExists {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Description
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "$Description nao encontrado: $Path"
    }
}

function Assert-CommandAvailable {
    param(
        [Parameter(Mandatory = $true)]
        [string] $CommandName
    )

    if (-not (Get-Command $CommandName -ErrorAction SilentlyContinue)) {
        throw "Comando nao encontrado no PATH: $CommandName"
    }
}

function Assert-NoExtraArguments {
    param(
        [Parameter(Mandatory = $true)]
        [string] $CommandName
    )

    if ($script:RestArguments.Count -gt 0) {
        throw "O comando '$CommandName' nao aceita argumentos extras: $($script:RestArguments -join ' ')"
    }
}

function Get-BananaSuisaAppProcessesFromRepo {
    $root = [System.IO.Path]::GetFullPath($script:ProjectRoot)
    $list = @()
    foreach ($p in (Get-Process -Name 'BananaSuisa', 'BananaSuisa.App' -ErrorAction SilentlyContinue)) {
        $exePath = $null
        try {
            $exePath = $p.Path
            if (-not $exePath -and $p.MainModule) {
                $exePath = $p.MainModule.FileName
            }
        } catch {
            # Processo protegido
        }

        if ([string]::IsNullOrWhiteSpace($exePath)) {
            # Sem permissao para ler o caminho (ex: app iniciada com UAC, script rodando normal).
            # Assumimos que e a app deste repositorio para nao ignorar a falha de locks em processos elevados.
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

function Stop-BananaSuisaAppIfRunning {
    <#
    Encerra o executavel da UI deste repositorio se estiver aberto. Sem isso, o dotnet build
    falha (MSB3027/MSB3021) ao copiar DLLs para bin\ porque o .exe mantem os ficheiros bloqueados.
    #>
    $procs = Get-BananaSuisaAppProcessesFromRepo
    if ($procs.Count -eq 0) {
        return
    }

    foreach ($p in $procs) {
        Write-Host "Encerrando instancia anterior (PID $($p.Id)) para o build poder atualizar os ficheiros..." -ForegroundColor Yellow
        try {
            Stop-Process -Id $p.Id -Force -ErrorAction Stop
        } catch {
            Write-Warning "Nao foi possivel encerrar o processo (pode estar elevado por UAC). Feche a janela do BananaSuisa manualmente e execute o build de novo."
        }
    }

    Start-Sleep -Milliseconds 400
}

function Invoke-RunResidualCleanup {
    <#
    Executado sempre no comando 'run': verifica e encerra processos residuais deste repositorio,
    com varias tentativas para libertar locks antes do dotnet build.
    #>
    Write-Host ''
    Write-Host 'Verificando processos residuais (BananaSuisa.App neste repositorio)...' -ForegroundColor Cyan

    $maxRounds = 3
    for ($round = 1; $round -le $maxRounds; $round++) {
        $remaining = @(Get-BananaSuisaAppProcessesFromRepo)
        if ($remaining.Count -eq 0) {
            if ($round -eq 1) {
                Write-Host 'Nenhum processo residual encontrado.' -ForegroundColor DarkGray
            } else {
                Write-Host 'Processos residuais encerrados.' -ForegroundColor Green
            }
            return
        }

        if ($round -gt 1) {
            Write-Host "Tentativa $round de ${maxRounds} (ainda $($remaining.Count) processo(s))..." -ForegroundColor DarkYellow
            Start-Sleep -Milliseconds 600
        }

        Stop-BananaSuisaAppIfRunning
    }

    $still = @(Get-BananaSuisaAppProcessesFromRepo)
    if ($still.Count -gt 0) {
        Write-Warning "Ainda ha $($still.Count) processo(s) deste repo em execucao. Feche a(s) janela(s) do BananaSuisa e volte a executar '.\bs.cmd run'."
    }
}

function Invoke-DotNetCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments
    )

    Assert-CommandAvailable 'dotnet'

    Push-Location $script:ProjectRoot
    try {
        & dotnet @Arguments
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-SolutionBuild {
    Assert-PathExists -Path $script:SolutionPath -Description 'Solution .NET'
    Stop-BananaSuisaAppIfRunning
    Invoke-DotNetCommand -Arguments (@('build', $script:SolutionPath) + $script:RestArguments)
}

function Invoke-AppRun {
    Assert-PathExists -Path $script:AppProjectPath -Description 'Projeto da aplicacao'

    # Sempre antes do build: libertar DLLs em bin\ (evita MSB3027)
    Invoke-RunResidualCleanup

    # 1. Compila para garantir que o .exe esta atualizado
    Invoke-DotNetCommand -Arguments (@('build', $script:AppProjectPath) + $script:RestArguments)
    
    # 2. Tenta iniciar o executavel forçando o prompt de administrador (UAC)
    $exePath = Join-Path $script:ProjectRoot 'src\BananaSuisa.App\bin\Debug\net10.0-windows\BananaSuisa.exe'
    
    if (Test-Path -LiteralPath $exePath) {
        Write-Host "`nSolicitando privilegios de Administrador (Verifique o prompt do UAC)..." -ForegroundColor Cyan
        Start-Process -FilePath $exePath -Verb RunAs
    } else {
        throw "Executavel nao encontrado apos o build: $exePath"
    }
}

function Invoke-SolutionTests {
    Assert-PathExists -Path $script:SolutionPath -Description 'Solution .NET'
    Invoke-DotNetCommand -Arguments (@('test', $script:SolutionPath) + $script:RestArguments)
}

function Invoke-FullCheck {
    Assert-NoExtraArguments 'check'

    Stop-BananaSuisaAppIfRunning
    Invoke-DotNetCommand -Arguments @('build', $script:SolutionPath)
    Invoke-DotNetCommand -Arguments @('test', $script:SolutionPath)
}

function Get-AppVersionFromSource {
    $versionFile = Join-Path $script:ProjectRoot 'src\BananaSuisa.Core\Versioning\AppVersion.cs'
    if (Test-Path -LiteralPath $versionFile) {
        $content = Get-Content -LiteralPath $versionFile -Raw
        if ($content -match '"(\d+\.\d+\.\d+)"') {
            return $Matches[1]
        }
    }
    return '0.0.0'
}

function Invoke-AppPublish {
    <#
    Gera executavel autonomo num unico ficheiro (self-contained win-x64, Release, single-file com compressao).
    O nome do .exe inclui a versao (ex: BananaSuisa_0.3.0.exe) para que versoes anteriores
    coexistam sem serem substituidas pelo Windows.
    #>
    Assert-PathExists -Path $script:AppProjectPath -Description 'Projeto da aplicacao'

    $version = Get-AppVersionFromSource
    $outDir = Join-Path $script:ProjectRoot 'artifacts\publish'
    if (Test-Path -LiteralPath $outDir) {
        Write-Host "A limpar saida anterior: $outDir" -ForegroundColor DarkYellow
        Remove-Item -LiteralPath $outDir -Recurse -Force -ErrorAction Stop
    }
    New-Item -ItemType Directory -Path $outDir -Force | Out-Null

    Stop-BananaSuisaAppIfRunning

    $publishArgs = @(
        'publish', $script:AppProjectPath,
        '-c', 'Release',
        '-r', 'win-x64',
        '--self-contained', 'true',
        '-p:PublishSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true',
        '-p:EnableCompressionInSingleFile=true',
        '-p:PublishReadyToRun=true',
        '-p:DebugType=None',
        '-p:DebugSymbols=false',
        '-o', $outDir
    ) + $script:RestArguments

    Invoke-DotNetCommand -Arguments $publishArgs

    $originalExe = Join-Path $outDir 'BananaSuisa.exe'
    if (-not (Test-Path -LiteralPath $originalExe)) {
        throw "Publicacao concluida mas o executavel nao foi encontrado: $originalExe"
    }

    $versionedName = "BananaSuisa_${version}.exe"
    $versionedExe = Join-Path $outDir $versionedName
    Move-Item -LiteralPath $originalExe -Destination $versionedExe -Force

    # Assinar o executavel com certificado de desenvolvimento (Dioner Frigi)
    $cert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Where-Object { $_.Subject -eq 'CN=Dioner Frigi' } | Select-Object -First 1
    if ($cert) {
        Write-Host "Assinando executavel com certificado '$($cert.Subject)' ..." -ForegroundColor Cyan
        try {
            Set-AuthenticodeSignature -FilePath $versionedExe -Certificate $cert -TimestampServer 'http://timestamp.digicert.com' -HashAlgorithm SHA256 -ErrorAction Stop | Out-Null
            Write-Host "  Assinatura aplicada. Fornecedor no UAC: Dioner Frigi" -ForegroundColor Green
        } catch {
            Write-Warning "Falha ao assinar (o .exe funciona sem assinatura): $_"
        }
    } else {
        Write-Warning "Certificado 'CN=Dioner Frigi' nao encontrado. Execute: .\ferramentas\create-dev-cert.ps1"
    }

    Write-Host ''
    Write-Host "Publicacao concluida (ficheiro unico, v${version})." -ForegroundColor Green
    Write-Host "  Executavel: $versionedExe" -ForegroundColor Cyan
    Write-Host "  Desenvolvedor: Dioner Frigi" -ForegroundColor DarkGray
}

function Show-BananaSuisaCliHelp {
    Write-Host @'
BananaSuisa CLI — tarefas de desenvolvimento

Comandos:
  build, compilar               Executa dotnet build BananaSuisa.slnx (encerra a UI deste repo se estiver aberta)
  run, rodar, ui                Sempre verifica/encerra processos residuais deste repo, compila e abre a UI
  test, testar                  Executa dotnet test BananaSuisa.slnx
  check, validar                Executa build + test
  publish, empacotar, package   Publica a app WPF num unico .exe (Release, win-x64, self-contained, single-file) em artifacts\publish\
  help                          Esta ajuda

Exemplos:
  .\bs.cmd build
  .\bs.cmd run
  .\bs.cmd test
  .\bs.cmd check
  .\bs.cmd publish

'@
}

switch -Regex ($Command.ToLowerInvariant()) {
    '^(build|compilar|gerar|build-dotnet|dotnet-build)$' {
        Invoke-SolutionBuild
    }
    '^(run|rodar|ui)$' {
        Invoke-AppRun
    }
    '^(test|testar)$' {
        Invoke-SolutionTests
    }
    '^(check|validar)$' {
        Invoke-FullCheck
    }
    '^(publish|empacotar|package)$' {
        Invoke-AppPublish
    }
    '^help$|^(\?|-h|--help)$' {
        Assert-NoExtraArguments 'help'
        Show-BananaSuisaCliHelp
    }
    default {
        Write-Warning "Comando desconhecido: $Command"
        Show-BananaSuisaCliHelp
        exit 1
    }
}
