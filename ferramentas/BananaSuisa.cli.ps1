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
    foreach ($p in (Get-Process -Name 'BananaSuisa.App' -ErrorAction SilentlyContinue)) {
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
    $exePath = Join-Path $script:ProjectRoot 'src\BananaSuisa.App\bin\Debug\net10.0-windows\BananaSuisa.App.exe'
    
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

function Show-BananaSuisaCliHelp {
    Write-Host @'
BananaSuisa CLI — tarefas de desenvolvimento

Comandos:
  build, compilar               Executa dotnet build BananaSuisa.slnx (encerra a UI deste repo se estiver aberta)
  run, rodar, ui                Sempre verifica/encerra processos residuais deste repo, compila e abre a UI
  test, testar                  Executa dotnet test BananaSuisa.slnx
  check, validar                Executa build + test
  help                          Esta ajuda

Exemplos:
  .\bs.cmd build
  .\bs.cmd run
  .\bs.cmd test
  .\bs.cmd check

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
