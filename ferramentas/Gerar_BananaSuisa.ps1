#Requires -Version 5.1
# ferramentas/Gerar_BananaSuisa.ps1 — consolida modulos em BananaSuisa.ps1 (versao em nucleo/versao.ps1)

$ErrorActionPreference = "Stop"
$buildRoot = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$projectRoot = Split-Path -Parent $buildRoot
$outputFile = Join-Path $projectRoot "BananaSuisa.ps1"
$versaoPath = Join-Path $projectRoot "BananaSuisa_desenvolvimento\nucleo\versao.ps1"

if (-not (Test-Path $versaoPath)) {
    throw "Arquivo de versao obrigatorio nao encontrado: $versaoPath"
}
. $versaoPath
$versaoStr = $script:BananaSuisaVersao
if ([string]::IsNullOrWhiteSpace($versaoStr)) {
    throw "BananaSuisaVersao vazio em versao.ps1"
}

# Remove geracao anterior (consolidado e nome legado PRO)
foreach ($old in @(
        (Join-Path $projectRoot "BananaSuisa.ps1"),
        (Join-Path $projectRoot "BananaSuisa_PRO.ps1")
    )) {
    if (Test-Path -LiteralPath $old) {
        Remove-Item -LiteralPath $old -Force
        Write-Host "[x] Removido anterior: $old" -ForegroundColor DarkYellow
    }
}

$moduleFiles = @(
    @{ Path = "BananaSuisa_desenvolvimento\nucleo\bootstrap.ps1";  Region = "Core_Bootstrap" },
    @{ Path = "BananaSuisa_desenvolvimento\interface\theme.ps1";       Region = "UI_Theme" },
    @{ Path = "BananaSuisa_desenvolvimento\funcionalidades\search.ps1";  Region = "Features_Search" },
    @{ Path = "BananaSuisa_desenvolvimento\interface\layout.ps1";      Region = "UI_Layout" },
    @{ Path = "BananaSuisa_desenvolvimento\funcionalidades\catalog.ps1"; Region = "Features_Catalog" },
    @{ Path = "BananaSuisa_desenvolvimento\interface\views.ps1";       Region = "UI_Views" },
    @{ Path = "BananaSuisa_desenvolvimento\funcionalidades\actions.ps1"; Region = "Features_Actions" },
    @{ Path = "BananaSuisa_desenvolvimento\eventos\app.events.ps1";     Region = "App_Events" }
)

Write-Host "--- Gerar BananaSuisa v$versaoStr ---" -ForegroundColor Cyan

$builder = New-Object System.Text.StringBuilder
[void]$builder.AppendLine("#Requires -Version 5.1")
[void]$builder.AppendLine("# ===========================================================================")
[void]$builder.AppendLine("# BANANASUISA - Script consolidado")
[void]$builder.AppendLine("# Gerado em: $(Get-Date -Format 'dd/MM/yyyy HH:mm:ss')")
[void]$builder.AppendLine("# Versao: $versaoStr")
[void]$builder.AppendLine("# ===========================================================================")
[void]$builder.AppendLine("")
[void]$builder.AppendLine("# Versao embutida (espelha BananaSuisa_desenvolvimento/nucleo/versao.ps1)")
[void]$builder.AppendLine("`$script:BananaSuisaVersao = '$versaoStr'")
[void]$builder.AppendLine("")

foreach ($mod in $moduleFiles) {
    $modulePath = Join-Path $projectRoot $mod.Path
    if (-not (Test-Path $modulePath)) {
        Write-Host "[!] Modulo ausente: $($mod.Path)" -ForegroundColor Red
        continue
    }

    Write-Host "  [+] Mesclando: $($mod.Path) [Region: $($mod.Region)]" -ForegroundColor Gray
    
    $content = Get-Content $modulePath -Raw -Encoding UTF8
    $content = $content -replace '(?m)^#Requires.*', ''
    
    [void]$builder.AppendLine("#region $($mod.Region)")
    [void]$builder.AppendLine($content.Trim())
    [void]$builder.AppendLine("#endregion")
    [void]$builder.AppendLine("")
}

Write-Host "Salvando BananaSuisa.ps1 (UTF-8)..." -ForegroundColor Cyan
[System.IO.File]::WriteAllText($outputFile, $builder.ToString(), [System.Text.Encoding]::UTF8)

Write-Host "Build concluido: $outputFile" -ForegroundColor Green
