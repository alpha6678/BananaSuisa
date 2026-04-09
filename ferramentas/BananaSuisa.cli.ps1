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
    Invoke-DotNetCommand -Arguments (@('build', $script:SolutionPath) + $script:RestArguments)
}

function Invoke-AppRun {
    Assert-PathExists -Path $script:AppProjectPath -Description 'Projeto da aplicacao'
    Invoke-DotNetCommand -Arguments (@('run', '--project', $script:AppProjectPath) + $script:RestArguments)
}

function Invoke-SolutionTests {
    Assert-PathExists -Path $script:SolutionPath -Description 'Solution .NET'
    Invoke-DotNetCommand -Arguments (@('test', $script:SolutionPath) + $script:RestArguments)
}

function Invoke-FullCheck {
    Assert-NoExtraArguments 'check'

    Invoke-DotNetCommand -Arguments @('build', $script:SolutionPath)
    Invoke-DotNetCommand -Arguments @('test', $script:SolutionPath)
}

function Show-BananaSuisaCliHelp {
    Write-Host @'
BananaSuisa CLI — tarefas de desenvolvimento

Comandos:
  build, compilar               Executa dotnet build BananaSuisa.slnx
  run, rodar, ui                Executa a UI WPF com dotnet run
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
