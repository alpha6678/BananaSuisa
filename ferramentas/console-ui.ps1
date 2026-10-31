#Requires -Version 5.1
# Feedback de terminal do rb: progresso numa linha, painel de etapas e filtro de ruido.
# Quando a saida esta redirecionada, ou RIBANENSE_PLAIN=1, cai para linhas simples.

$script:RbUi = @{
    Enabled     = $null
    BlockLines  = @()
    BlockStates = @()
    BlockStart  = @{}
    BlockEnd    = @{}
    Rendered    = 0
    ProgressLen = 0
}

function Test-RbUiEnabled {
    if ($null -ne $script:RbUi.Enabled) { return [bool]$script:RbUi.Enabled }
    $enabled = $true
    if ($env:RIBANENSE_PLAIN -eq '1' -or $env:RIBANENSE_UI -eq 'plain') { $enabled = $false }
    if ($env:RIBANENSE_UI -eq 'force') { $script:RbUi.Enabled = $true; return $true }
    try {
        if ([Console]::IsOutputRedirected) { $enabled = $false }
    }
    catch {
        $enabled = $false
    }
    $script:RbUi.Enabled = $enabled
    return $enabled
}

function Test-RbConsoleCanDraw {
    # SetCursorPosition falha quando a saida nao e um console de verdade.
    try {
        $top = [Console]::CursorTop
        [Console]::SetCursorPosition(0, $top)
        return $true
    }
    catch {
        $script:RbUi.Enabled = $false
        return $false
    }
}

function Get-RbConsoleWidth {
    try {
        $w = [Console]::WindowWidth
        if ($w -gt 20) { return $w }
    }
    catch { }
    return 100
}

function Format-RbDuration {
    param([double] $Seconds)
    if ($Seconds -lt 10) { return ('{0:0.0}s' -f $Seconds) }
    if ($Seconds -lt 60) { return ('{0:0}s' -f $Seconds) }
    $min = [Math]::Floor($Seconds / 60)
    $sec = [Math]::Floor($Seconds % 60)
    return ('{0}m{1:00}s' -f $min, $sec)
}

function ConvertTo-RbLine {
    param([int] $Length)
    $width = Get-RbConsoleWidth
    $max = [Math]::Max(20, $width - 1)
    if ($Length -gt $max) { return $max }
    return $Length
}

# ---------- Linha unica que se redesenha ----------

function Clear-RbProgress {
    if (-not (Test-RbUiEnabled)) { return }
    if ($script:RbUi.ProgressLen -le 0) { return }
    try {
        [Console]::Write("`r" + (' ' * $script:RbUi.ProgressLen) + "`r")
    }
    catch {
        $script:RbUi.Enabled = $false
    }
    $script:RbUi.ProgressLen = 0
}

function Write-RbProgress {
    param(
        [Parameter(Mandatory)] [string] $Text,
        [string] $Color = 'Cyan'
    )
    # Sem terminal interativo nao ha o que redesenhar: melhor nao gastar linha.
    if (-not (Test-RbUiEnabled)) { return }
    $max = ConvertTo-RbLine -Length $Text.Length
    $line = $Text
    if ($line.Length -gt $max - 1) { $line = $line.Substring(0, $max - 1) + '…' }
    $pad = [Math]::Max($script:RbUi.ProgressLen, $line.Length)
    $previous = [Console]::ForegroundColor
    try {
        if ($Color) { [Console]::ForegroundColor = [ConsoleColor]::$Color }
        [Console]::Write("`r" + $line.PadRight($pad))
    }
    catch {
        $script:RbUi.Enabled = $false
        return
    }
    finally {
        try { [Console]::ForegroundColor = $previous } catch { }
    }
    $script:RbUi.ProgressLen = $pad
}

function Write-RbTick {
    param([string] $Text, [string] $Color = 'Gray')
    Clear-RbProgress
    Write-Host $Text -ForegroundColor $Color
}

function Test-RbVerbose {
    if ($script:RestArguments -and ($script:RestArguments -contains '-Verbose')) { return $true }
    return $false
}

# ---------- Bloco de etapas que se redesenha ----------

function Get-RbStepIcon {
    param([string] $State)
    switch ($State) {
        'running' { return '[..]' }
        'ok'      { return '[OK]' }
        'fail'    { return '[!!]' }
        'skip'    { return '[--]' }
        default   { return '[  ]' }
    }
}

function Get-RbStepColor {
    param([string] $State)
    switch ($State) {
        'running' { return 'Cyan' }
        'ok'      { return 'Green' }
        'fail'    { return 'Red' }
        'skip'    { return 'DarkGray' }
        default   { return 'DarkGray' }
    }
}

function Get-RbStepLines {
    # Monta as linhas do bloco. Funcao pura: facilita testar sem console.
    param([int] $MaxWidth = 0)
    if ($MaxWidth -le 0) { $MaxWidth = [Math]::Max(20, (Get-RbConsoleWidth) - 1) }
    $lines = @()
    for ($i = 0; $i -lt $script:RbUi.BlockLines.Count; $i++) {
        $state = $script:RbUi.BlockStates[$i]
        $text = '{0} {1}' -f (Get-RbStepIcon -State $state), $script:RbUi.BlockLines[$i]
        if ($script:RbUi.BlockStart.ContainsKey($i) -and $script:RbUi.BlockEnd.ContainsKey($i)) {
            $elapsed = $script:RbUi.BlockEnd[$i].Subtract($script:RbUi.BlockStart[$i]).TotalSeconds
            $text += ' (' + (Format-RbDuration -Seconds $elapsed) + ')'
        }
        elseif ($state -eq 'running' -and $script:RbUi.BlockStart.ContainsKey($i)) {
            $elapsed = (Get-Date).Subtract($script:RbUi.BlockStart[$i]).TotalSeconds
            if ($elapsed -ge 1) { $text += ' (' + (Format-RbDuration -Seconds $elapsed) + ')' }
        }
        if ($text.Length -gt $MaxWidth - 1) { $text = $text.Substring(0, $MaxWidth - 1) + '…' }
        $lines += [pscustomobject]@{ Text = $text; Color = (Get-RbStepColor -State $state) }
    }
    return @($lines)
}

function Write-RbStepBlock {
    if (-not (Test-RbUiEnabled)) { return }
    if (-not (Test-RbConsoleCanDraw)) { return }
    $max = [Math]::Max(20, (Get-RbConsoleWidth) - 1)
    $lines = @(Get-RbStepLines -MaxWidth $max)

    $top = [Console]::CursorTop - $script:RbUi.Rendered
    if ($top -lt 0) { $top = 0 }
    $previous = [Console]::ForegroundColor
    try {
        [Console]::SetCursorPosition(0, $top)
        foreach ($line in $lines) {
            [Console]::ForegroundColor = [ConsoleColor]::$($line.Color)
            [Console]::Write($line.Text.PadRight($max))
            [Console]::Write("`n")
        }
    }
    catch {
        $script:RbUi.Enabled = $false
        return
    }
    finally {
        try { [Console]::ForegroundColor = $previous } catch { }
    }
    $script:RbUi.Rendered = $lines.Count
    $script:RbUi.ProgressLen = 0
}

function Start-RbStepBlock {
    param([Parameter(Mandatory)] [string[]] $Steps)
    Clear-RbProgress
    $script:RbUi.BlockLines = @($Steps)
    $script:RbUi.BlockStates = @(foreach ($s in $Steps) { 'pending' })
    $script:RbUi.BlockStart = @{}
    $script:RbUi.BlockEnd = @{}
    $script:RbUi.Rendered = 0
    if (Test-RbUiEnabled) { Write-RbStepBlock }
}

function Update-RbStep {
    param(
        [Parameter(Mandatory)] [int] $Index,
        [Parameter(Mandatory)] [ValidateSet('pending', 'running', 'ok', 'fail', 'skip')] [string] $State,
        [string] $Text
    )
    if ($Index -lt 0 -or $Index -ge $script:RbUi.BlockLines.Count) { return }
    if ($Text) { $script:RbUi.BlockLines[$Index] = $Text }
    $script:RbUi.BlockStates[$Index] = $State
    if ($State -eq 'running') { $script:RbUi.BlockStart[$Index] = Get-Date }
    if ($State -in @('ok', 'fail', 'skip')) { $script:RbUi.BlockEnd[$Index] = Get-Date }

    if (-not (Test-RbUiEnabled)) {
        if ($State -in @('ok', 'fail', 'skip')) {
            $suffix = ''
            if ($script:RbUi.BlockStart.ContainsKey($Index)) {
                $elapsed = $script:RbUi.BlockEnd[$Index].Subtract($script:RbUi.BlockStart[$Index]).TotalSeconds
                $suffix = ' (' + (Format-RbDuration -Seconds $elapsed) + ')'
            }
            $line = '{0} {1}{2}' -f (Get-RbStepIcon -State $State), $script:RbUi.BlockLines[$Index], $suffix
            Write-Host $line -ForegroundColor (Get-RbStepColor -State $State)
        }
        return
    }
    Write-RbStepBlock
}

function Stop-RbStepBlock {
    Clear-RbProgress
    $script:RbUi.Rendered = 0
    $script:RbUi.BlockLines = @()
    $script:RbUi.BlockStates = @()
    $script:RbUi.BlockStart = @{}
    $script:RbUi.BlockEnd = @{}
}

# ---------- Filtro de ruido ----------

function Test-RbNoisyLine {
    param([Parameter(Mandatory)] [AllowEmptyString()] [string] $Line)
    $t = $Line.Trim()
    if ($t.Length -eq 0) { return $true }
    if ($t.StartsWith('-- Components:')) { return $true }
    if ($t.StartsWith('-- Component paths:')) { return $true }
    if ($t.StartsWith('-- Found Git:')) { return $true }
    if ($t.StartsWith('-- Building ESP-IDF components')) { return $true }
    if ($t.StartsWith('-- Project sdkconfig')) { return $true }
    if ($t.StartsWith('-- Compiler supported targets')) { return $true }
    if ($t.StartsWith('-- Adding linker script')) { return $true }
    if ($t.StartsWith('-- Bootloader project name')) { return $true }
    if ($t -eq '-- Configuring done') { return $true }
    if ($t -eq '-- Generating done') { return $true }
    if ($t.StartsWith('-- Build files have been written')) { return $true }
    if ($t.StartsWith('Enumerating objects:')) { return $true }
    if ($t.StartsWith('Counting objects:')) { return $true }
    if ($t.StartsWith('Delta compression')) { return $true }
    if ($t.StartsWith('Compressing objects:')) { return $true }
    if ($t -match '^Writing objects:.*done\.$') { return $true }
    if ($t -match '^Total \d+ \(delta') { return $true }
    if ($t.StartsWith('remote: Resolving deltas')) { return $true }
    if ($t -match '^To (https?://|git@)') { return $true }
    return $false
}

# ---------- Execucao externa com progresso ----------

function Invoke-RbExternal {
    <#
      Executa um scriptblock e mostra o andamento numa linha so.
      - ProgressPattern: regex com grupo 1 = atual e 2 = total (ex.: [123/1488]).
      - Tail: quantas linhas uteis guardar para relatar em caso de falha.
      Falha vem de excecao do scriptblock (os scripts do rb ja lancam em erro),
      nao de texto em stderr: dotnet e idf.py escrevem aviso ali.
      Devolve as linhas uteis capturadas.
    #>
    param(
        [Parameter(Mandatory)] [scriptblock] $Script,
        [string] $Label = '',
        [string] $ProgressPattern = '',
        [int] $Tail = 12,
        [string] $FailurePrefix = 'comando falhou'
    )
    $kept = New-Object System.Collections.Generic.List[string]
    $caught = $null

    try {
        & $Script 2>&1 | ForEach-Object {
            $chunk = ($_ | Out-String).TrimEnd()
            foreach ($line in ($chunk -split "`r?`n")) {
                if (Test-RbNoisyLine -Line $line) { continue }
                $isProgress = $false
                if ($ProgressPattern -and $line -match $ProgressPattern) {
                    $current = $Matches[1]
                    $total = $Matches[2]
                    $pct = 0
                    if ([int]$total -gt 0) { $pct = [int](100 * [int]$current / [int]$total) }
                    Write-RbProgress -Text ('{0} {1}/{2} ({3}%)' -f $Label, $current, $total, $pct)
                    $isProgress = $true
                }
                if (-not $isProgress) {
                    $kept.Add($line.TrimEnd())
                    if ($kept.Count -gt $Tail) { $kept.RemoveAt(0) }
                }
            }
        }
    }
    catch {
        $caught = $_
    }
    Clear-RbProgress

    if ($caught) {
        foreach ($line in $kept) { Write-Host ("     " + $line) -ForegroundColor DarkGray }
        throw "$FailurePrefix. $($caught.Exception.Message)"
    }
    return @($kept)
}
