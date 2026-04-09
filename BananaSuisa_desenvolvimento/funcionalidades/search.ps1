#region [05-SEARCH] Busca, debounce e filtros anti-loop

# ============================================
# HEADER (70px) - Apenas titulo
# ============================================
$Header = New-Object System.Windows.Forms.Panel
$Header.Dock = "Top"
$Header.Height = 70
$Header.BackColor = $Panel
$Form.Controls.Add($Header)

# Campo de busca no Header (alinhado a direita, inicialmente oculto)
$SearchBox = New-Object System.Windows.Forms.TextBox
$SearchBox.Font = New-Object System.Drawing.Font("Segoe UI", 10)
$SearchBox.BackColor = $ItemBG
$SearchBox.ForeColor = $TextDim
$SearchBox.BorderStyle = "FixedSingle"
$SearchBox.Size = New-Object System.Drawing.Size(250, 26)
$SearchBox.Visible = $false
$Header.Controls.Add($SearchBox)

$SearchPlaceholder = "Buscar..."
$SearchBox.Text = $SearchPlaceholder
$SearchBox.ForeColor = $TextDim

$script:SearchFilterState = @{
    LastRequested = ""
    LastApplied = ""
    IsFiltering = $false
    Pending = $false
    IgnoreTextChange = $false
}

$script:SearchDebounceTimer = New-Object System.Windows.Forms.Timer
$script:SearchDebounceTimer.Interval = 220

function Set-SearchBoxText {
    param(
        [string]$Text,
        [System.Drawing.Color]$ForeColor = [System.Drawing.Color]::Empty
    )
    if ($SearchBox.Text -eq $Text -and $ForeColor -eq [System.Drawing.Color]::Empty) { return }

    $script:SearchFilterState.IgnoreTextChange = $true
    try {
        $SearchBox.Text = $Text
        if ($ForeColor -ne [System.Drawing.Color]::Empty) {
            $SearchBox.ForeColor = $ForeColor
        }
    } finally {
        $script:SearchFilterState.IgnoreTextChange = $false
    }

    $script:SearchFilterState.LastRequested = $Text
    if ($script:SearchDebounceTimer.Enabled) { $script:SearchDebounceTimer.Stop() }
    if ($SearchBox.Visible) { $script:SearchDebounceTimer.Start() }
}

$script:SearchDebounceTimer.Add_Tick({
    $script:SearchDebounceTimer.Stop()

    if ($Form.IsDisposed -or $SearchBox.IsDisposed -or -not $SearchBox.Visible) { return }
    if ($script:SearchFilterState.IgnoreTextChange) { return }

    $searchText = $script:SearchFilterState.LastRequested
    if ($searchText -eq $SearchPlaceholder) { $searchText = "" }
    if ($searchText -eq $script:SearchFilterState.LastApplied) { return }

    if ($script:SearchFilterState.IsFiltering) {
        $script:SearchFilterState.Pending = $true
        return
    }

    $script:SearchFilterState.IsFiltering = $true
    try {
        & $script:FilterItems $searchText
        $script:SearchFilterState.LastApplied = $searchText
    } finally {
        $script:SearchFilterState.IsFiltering = $false
        if ($script:SearchFilterState.Pending) {
            $script:SearchFilterState.Pending = $false
            $script:SearchFilterState.LastRequested = $SearchBox.Text
            if ($SearchBox.Visible) { $script:SearchDebounceTimer.Start() }
        }
    }
})

# Placeholder behavior
$SearchBox.Add_GotFocus({
    if ($SearchBox.Text -eq $SearchPlaceholder) {
        Set-SearchBoxText -Text "" -ForeColor $Text
    }
})

$SearchBox.Add_LostFocus({
    if ($SearchBox.Text -eq "") {
        Set-SearchBoxText -Text $SearchPlaceholder -ForeColor $TextDim
    }
})

# Funcao de filtro
# Funcao para normalizar texto (remover acentos e caracteres especiais)
function Get-NormalizedText {
    param([string]$text)
    if (-not $text) { return "" }
    
    $normalized = $text.ToLower()
    
    # Remover acentos comuns
    $normalized = $normalized -replace '[áàâãä]', 'a'
    $normalized = $normalized -replace '[éèêë]', 'e'
    $normalized = $normalized -replace '[íìîï]', 'i'
    $normalized = $normalized -replace '[óòôõö]', 'o'
    $normalized = $normalized -replace '[úùûü]', 'u'
    $normalized = $normalized -replace '[ç]', 'c'
    $normalized = $normalized -replace '[ñ]', 'n'
    
    return $normalized
}

# Funcao para verificar similaridade entre duas strings
function Test-StringSimilarity {
    param([string]$search, [string]$target)
    
    if (-not $search -or -not $target) { return $false }
    if ($search.Length -lt 2) { return $target -like "*$search*" }
    
    # Contar caracteres em comum na mesma posicao ou proxima
    $matchCount = 0
    $searchChars = $search.ToCharArray()
    $targetLower = $target.ToLower()
    
    for ($i = 0; $i -lt $searchChars.Length; $i++) {
        $char = $searchChars[$i]
        # Procurar o caractere na posicao esperada ou proxima (+/- 2)
        $startPos = [Math]::Max(0, $i - 2)
        $endPos = [Math]::Min($targetLower.Length - 1, $i + 2)
        
        for ($j = $startPos; $j -le $endPos; $j++) {
            if ($j -lt $targetLower.Length -and $targetLower[$j] -eq $char) {
                $matchCount++
                break
            }
        }
    }
    
    # Calcular percentual de match
    $ratio = $matchCount / $search.Length
    
    # Tolerancia: 70% de match para palavras curtas, 60% para longas
    $threshold = if ($search.Length -le 4) { 0.70 } else { 0.60 }
    
    return $ratio -ge $threshold
}

# Funcao para verificar similaridade (fuzzy match)
function Test-FuzzyMatch {
    param([string]$searchTerm, [string]$text)
    
    if (-not $searchTerm -or -not $text) { return $false }
    
    # Normalizar ambos os textos
    $searchNorm = Get-NormalizedText $searchTerm
    $textNorm = Get-NormalizedText $text
    
    # 1. Match exato ou contem
    if ($textNorm -like "*$searchNorm*") { return $true }
    
    # 2. Verificar match reverso (texto contem busca ou vice-versa)
    if ($searchNorm -like "*$textNorm*") { return $true }
    
    # 3. Verificar cada palavra da busca
    $searchWords = $searchNorm -split '\s+' | Where-Object { $_.Length -ge 2 }
    $textWords = $textNorm -split '[\s\.\-_]+' | Where-Object { $_.Length -ge 2 }
    
    foreach ($sw in $searchWords) {
        $found = $false
        
        foreach ($tw in $textWords) {
            # Match parcial (palavra comeca igual)
            if ($tw.StartsWith($sw) -or $sw.StartsWith($tw)) {
                $found = $true
                break
            }
            
            # Contem como substring
            if ($tw -like "*$sw*" -or $sw -like "*$tw*") {
                $found = $true
                break
            }
            
            # Fuzzy match por similaridade de caracteres
            if (Test-StringSimilarity -search $sw -target $tw) {
                $found = $true
                break
            }
        }
        
        # Se nenhuma palavra do texto matchou com esta palavra da busca
        if (-not $found) {
            # Tentar match direto no texto completo
            if (-not (Test-StringSimilarity -search $sw -target $textNorm)) {
                return $false
            }
        }
    }
    
    return $true
}

$script:FilterItems = {
    param($searchText)
    
    # Primeiro, mostrar/esconder itens de apps
    $showAll = $searchText -eq $SearchPlaceholder -or [string]::IsNullOrWhiteSpace($searchText)
    
    foreach ($item in $script:AppItems) {
        if ($showAll) {
            $item.Visible = $true
        } else {
            $app = $item.Tag.App
            $appName = if ($app.N) { $app.N } else { "" }
            $appId = if ($app.I) { $app.I } else { "" }
            $appCat = if ($app.C) { $app.C } else { "" }
            
            # Busca por similaridade (fuzzy): nome, ID ou categoria
            $visible = (Test-FuzzyMatch -searchTerm $searchText -text $appName) -or 
                       (Test-FuzzyMatch -searchTerm $searchText -text $appId) -or 
                       (Test-FuzzyMatch -searchTerm $searchText -text $appCat)
            
            $item.Visible = $visible
        }
    }
    
    # Agora, mostrar/esconder separadores de categoria baseado nos itens visiveis
    # Obter todos os controles do ListPanel
    $controls = $ListPanel.Controls
    $currentSeparator = $null
    $hasVisibleItems = $false
    
    for ($i = 0; $i -lt $controls.Count; $i++) {
        $ctrl = $controls[$i]
        
        # Verificar se e um separador
        if ($ctrl.Tag -and $ctrl.Tag.IsSeparator) {
            # Se tinha um separador anterior, definir visibilidade
            if ($currentSeparator -ne $null) {
                $currentSeparator.Visible = $hasVisibleItems -or $showAll
            }
            
            $currentSeparator = $ctrl
            $hasVisibleItems = $false
        } else {
            # E um item de app
            if ($ctrl.Visible) {
                $hasVisibleItems = $true
            }
        }
    }
    
    # Ultimo separador
    if ($currentSeparator -ne $null) {
        $currentSeparator.Visible = $hasVisibleItems -or $showAll
    }
}

# Evento de busca em tempo real
$SearchBox.Add_TextChanged({
    if ($script:SearchFilterState.IgnoreTextChange) { return }

    $script:SearchFilterState.LastRequested = $SearchBox.Text
    if ($script:SearchDebounceTimer.Enabled) { $script:SearchDebounceTimer.Stop() }
    $script:SearchDebounceTimer.Start()
})

#endregion
