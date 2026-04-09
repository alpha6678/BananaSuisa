#region [06-DATA] Catalogo auditado e mapeamentos

# ============================================
# LISTA DE APLICATIVOS HARDCODED
# ============================================
Write-FileLog "Carregando lista de aplicativos..." "INFO"

# Lista completa de apps (hardcoded) - apps instalados online serao salvos no JSON
$script:Apps = @(
    # === NAVEGADORES ===
    @{N = "Google Chrome"; I = "Google.Chrome"; C = "Navegadores"; E = $true }
    @{N = "Mozilla Firefox"; I = "Mozilla.Firefox"; C = "Navegadores"; E = $false }
    @{N = "Opera Browser"; I = "Opera.Opera"; C = "Navegadores"; E = $false }
    @{N = "Opera GX"; I = "Opera.OperaGX"; C = "Navegadores"; E = $false }
    
    # === ESCRITORIO ===
    @{N = "LibreOffice"; I = "TheDocumentFoundation.LibreOffice"; C = "Escritorio"; E = $false }
    @{N = "ONLYOFFICE"; I = "ONLYOFFICE.DesktopEditors"; C = "Escritorio"; E = $false }
    
    # === UTILITARIOS ===
    @{N = "7-Zip"; I = "7zip.7zip"; C = "Utilitarios"; E = $true }
    @{N = "Revo Uninstaller"; I = "RevoUninstaller.RevoUninstaller"; C = "Utilitarios"; E = $false }
    @{N = "Lightshot"; I = "Skillbrains.Lightshot"; C = "Utilitarios"; E = $false }
    @{N = "Flameshot"; I = "Flameshot.Flameshot"; C = "Utilitarios"; E = $false }
    
    # === ACESSO REMOTO ===
    @{N = "AnyDesk"; I = "AnyDesk.AnyDesk"; C = "Acesso Remoto"; E = $true }
    @{N = "TeamViewer"; I = "TeamViewer.TeamViewer"; C = "Acesso Remoto"; E = $false }
    @{N = "RustDesk"; I = "RustDesk.RustDesk"; C = "Acesso Remoto"; E = $false }
    
    # === MIDIA ===
    @{N = "OBS Studio"; I = "OBSProject.OBSStudio"; C = "Midia"; E = $false }
    
    # === DESIGN ===
    @{N = "GIMP"; I = "GIMP.GIMP.2"; C = "Design"; E = $false }
    @{N = "Krita"; I = "KDE.Krita"; C = "Design"; E = $false }
)

Write-FileLog "Apps hardcoded carregados: $($script:Apps.Count)" "INFO"

# Salvar copia da lista original hardcoded
$script:BaseApps = @($script:Apps)

# Carregar config para apps adicionais (catalogo e customizados do JSON)
$configApps = Get-AllAppsFromConfig
if ($configApps -and $configApps.Count -gt 0) {
    $addedCount = 0
    foreach ($app in $configApps) {
        if ($app.I -and $app.N) {
            # Verificar se nao existe ja no array (evitar duplicatas)
            $exists = $script:Apps | Where-Object { $_.I -eq $app.I }
            if (-not $exists) {
                $script:Apps += $app
                $addedCount++
            }
        }
    }
    Write-FileLog "Apps do JSON (catalogo/custom) adicionados: $addedCount" "INFO"
}

Write-FileLog "Total de apps na lista: $($script:Apps.Count)" "INFO"

# ============================================
# FUNCAO WRITE-LOG
# ============================================
# Variavel para rastrear a ultima linha de progresso
$script:LastProgressLine = -1
$script:LastProgressLength = 0

function Finalize-LogProgressLine {
    if ($script:LastProgressLine -lt 0) { return }
    
    $LogBox.SelectionStart = $LogBox.TextLength
    $LogBox.SelectionLength = 0
    
    if (-not $LogBox.Text.EndsWith("`r`n") -and -not $LogBox.Text.EndsWith("`n")) {
        $LogBox.AppendText("`r`n")
    }
    
    $script:LastProgressLine = -1
    $script:LastProgressLength = 0
}

function Write-Log {
    param(
        [string]$Message,
        [ValidateSet("Info", "Success", "Warning", "Error", "Progress")]
        [string]$Type = "Info"
    )
    
    $color = switch ($Type) {
        "Success"  { $Green }
        "Warning"  { $Yellow }
        "Error"    { $Red }
        "Progress" { $Blue }
        default    { $TextDim }
    }
    
    $prefix = switch ($Type) {
        "Success"  { "[OK] " }
        "Warning"  { "[!] " }
        "Error"    { "[X] " }
        "Progress" { "[...] " }
        default    { "" }
    }
    
    $timestamp = Get-Date -Format "HH:mm:ss"
    $fullMessage = "[$timestamp] $prefix$Message"
    
    Finalize-LogProgressLine
    $LogBox.SelectionStart = $LogBox.TextLength
    $LogBox.SelectionLength = 0
    $LogBox.SelectionColor = $color
    $LogBox.AppendText("$fullMessage`r`n")
    $LogBox.SelectionStart = $LogBox.TextLength
    $LogBox.ScrollToCaret()
    
    # Resetar tracking de progresso quando nova linha e adicionada
    $script:LastProgressLine = -1
    
    [System.Windows.Forms.Application]::DoEvents()
}

# Funcao para atualizar a ultima linha de log (para progresso dinamico)
function Update-LogProgress {
    param(
        [string]$Message,
        [string]$Progress = ""  # Ex: "[12MB/42MB]" ou "[50%]"
    )
    
    $timestamp = Get-Date -Format "HH:mm:ss"
    $fullMessage = if ($Progress) {
        "[$timestamp] [...] $Message $Progress"
    } else {
        "[$timestamp] [...] $Message"
    }
    
    # Se ja temos uma linha de progresso, substituir
    if ($script:LastProgressLine -ge 0 -and $script:LastProgressLength -gt 0) {
        $startPos = $script:LastProgressLine
        $LogBox.SelectionStart = $startPos
        $LogBox.SelectionLength = $script:LastProgressLength
        $LogBox.SelectionColor = $Blue
        $LogBox.SelectedText = $fullMessage
        $script:LastProgressLength = $fullMessage.Length
        $LogBox.SelectionStart = ($startPos + $script:LastProgressLength)
        $LogBox.SelectionLength = 0
    } else {
        # Nova linha de progresso
        $script:LastProgressLine = $LogBox.TextLength
        $LogBox.SelectionStart = $LogBox.TextLength
        $LogBox.SelectionLength = 0
        $LogBox.SelectionColor = $Blue
        $LogBox.AppendText($fullMessage)
        $script:LastProgressLength = $fullMessage.Length
    }
    
    $LogBox.ScrollToCaret()
    [System.Windows.Forms.Application]::DoEvents()
}

# Funcao de Download Assincrono (Multithread simulado para GUI)
function Invoke-WebDownload {
    param(
        [string]$Uri,
        [string]$OutFile,
        [string]$LogMessage = "Baixando..."
    )
    
    $wc = New-Object System.Net.WebClient
    $wc.Headers.Add("User-Agent", "PowerShell/5.1 (Windows NT 10.0; Win64; x64)")
    
    # Variaveis de estado sincronizadas
    $script:DownloadDone = $false
    $script:DownloadError = $null
    $script:DownloadPercent = 0
    $lastReportedPercent = -1
    
    # Eventos (agora apenas atualizam variaveis, sem mexer na UI diretamente)
    $wc.Add_DownloadProgressChanged({
        param($s, $e)
        $script:DownloadPercent = $e.ProgressPercentage
    })
    
    $wc.Add_DownloadFileCompleted({
        param($s, $e)
        if ($e.Error) { $script:DownloadError = $e.Error.Message }
        $script:DownloadDone = $true
    })
    
    try {
        $wc.DownloadFileAsync((New-Object Uri($Uri)), $OutFile)
        
        # Loop principal (Main Thread) - processa UI e atualiza log
        while (-not $script:DownloadDone) {
            if ($script:DownloadPercent -ne $lastReportedPercent) {
                Update-LogProgress $LogMessage "[$($script:DownloadPercent)%]"
                $lastReportedPercent = $script:DownloadPercent
            }
            [System.Windows.Forms.Application]::DoEvents()
            Start-Sleep -Milliseconds 100
        }
        
        if ($script:DownloadError) { throw $script:DownloadError }
        Update-LogProgress $LogMessage "[100%]"
        return $true
    } catch {
        throw $_
    } finally {
        $wc.Dispose()
    }
}

# Funcao para finalizar linha de progresso (adiciona quebra de linha)
function Complete-LogProgress {
    if ($script:LastProgressLine -ge 0) {
        Finalize-LogProgressLine
        $LogBox.SelectionStart = $LogBox.TextLength
        $LogBox.SelectionLength = 0
        $LogBox.ScrollToCaret()
        [System.Windows.Forms.Application]::DoEvents()
    }
}

# Funcao auxiliar para formatar bytes
function Format-Bytes {
    param([long]$Bytes)
    
    if ($Bytes -ge 1GB) { return "{0:N1}GB" -f ($Bytes / 1GB) }
    if ($Bytes -ge 1MB) { return "{0:N1}MB" -f ($Bytes / 1MB) }
    if ($Bytes -ge 1KB) { return "{0:N1}KB" -f ($Bytes / 1KB) }
    return "$Bytes B"
}

# ============================================
# FUNCAO UPDATE-STAGE
# ============================================
function Update-Stage {
    param([string]$Stage)
    $script:CurrentStage = $Stage
    $StageLabel.Text = $script:Stages[$Stage]
    $StageLabel.ForeColor = switch ($Stage) {
        "Completed" { $Green }
        "Failed" { $Red }
        "Downloading" { $Blue }
        "Installing" { $Orange }
        default { $TextDim }
    }
    [System.Windows.Forms.Application]::DoEvents()
}

# ============================================
# FUNCAO UPDATE-COUNT
# ============================================
$script:UpdateCount = {
    $count = @($script:Checkboxes | Where-Object { $_.Checked }).Count
    $LblCount.Text = "$count selecionados"
}

# ============================================
# FUNCAO PARA CRIAR SEPARADOR DE CATEGORIA
# ============================================
function New-CategorySeparator {
    param(
        [string]$Title,
        [int]$Count,
        [System.Drawing.Color]$Color
    )
    
    $itemWidth = [Math]::Max(500, $Form.ClientSize.Width - 200)
    
    $separator = New-Object System.Windows.Forms.Panel
    $separator.Size = New-Object System.Drawing.Size($itemWidth, 32)
    $separator.Margin = New-Object System.Windows.Forms.Padding(0, 10, 0, 5)
    $separator.BackColor = [System.Drawing.Color]::FromArgb(35, 35, 45)
    
    $lbl = New-Object System.Windows.Forms.Label
    $lbl.Text = "$Title ($Count)"
    $lbl.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 10)
    $lbl.ForeColor = $Color
    $lbl.Dock = "Fill"
    $lbl.TextAlign = "MiddleLeft"
    $lbl.Padding = New-Object System.Windows.Forms.Padding(10, 0, 0, 0)
    $separator.Controls.Add($lbl)
    
    # Marcar como separador para nao ser filtrado
    $separator.Tag = @{ IsSeparator = $true }
    
    return $separator
}

# ============================================
# FUNCAO PARA CRIAR ITEM DA LISTA
# ============================================
function New-AppItem {
    param(
        [hashtable]$App,
        [string]$ExtraInfo = "",
        [bool]$IsEssential = $false,
        [string]$Source = "list",  # "list", "update", "system"
        [bool]$IsInstalled = $false  # Para modo Install: app ja instalado
    )
    
    $itemHeight = 32
    # Usar largura inicial generosa - sera ajustada pelo Update-Layout
    $itemWidth = [Math]::Max(500, $Form.ClientSize.Width - 200)
    
    $item = New-Object System.Windows.Forms.TableLayoutPanel
    $item.Size = New-Object System.Drawing.Size($itemWidth, $itemHeight)
    $item.Margin = New-Object System.Windows.Forms.Padding(0, 1, 0, 1)
    
    # Cor de fundo: cinza escuro se ja instalado (exceto no modo Cache, onde queremos baixar mesmo assim)
    if ($IsInstalled -and $script:CurrentMode -ne "ManageInstallers") {
        $item.BackColor = [System.Drawing.Color]::FromArgb(35, 35, 40)
    } else {
        $item.BackColor = if ($IsEssential) { $ItemEss } else { $ItemBG }
    }
    
    $item.Cursor = if ($IsInstalled -and $script:CurrentMode -ne "ManageInstallers") { "Default" } else { "Hand" }
    $item.ColumnCount = 3
    $item.RowCount = 1
    # Coluna 1: Checkbox (35px), Coluna 2: Nome (expande), Coluna 3: Categoria (100px)
    $item.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle("Absolute", 35))) | Out-Null
    $item.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle("Percent", 100))) | Out-Null
    $item.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle("Absolute", 100))) | Out-Null
    $item.RowStyles.Add((New-Object System.Windows.Forms.RowStyle("Percent", 100))) | Out-Null
    $item.Tag = @{ App = $App; Source = $Source; IsInstalled = $IsInstalled }
    
    $cb = New-Object System.Windows.Forms.CheckBox
    $cb.Dock = "Fill"
    $cb.Margin = New-Object System.Windows.Forms.Padding(6, 0, 0, 0)
    # Preservar todos os metadados do item para a acao posterior
    $appWithSource = @{}
    foreach ($key in $App.Keys) {
        $appWithSource[$key] = $App[$key]
    }
    $appWithSource["Source"] = $Source
    $cb.Tag = $appWithSource
    $cb.Enabled = if ($script:CurrentMode -eq "ManageInstallers") { $true } else { -not $IsInstalled }
    $cb.Add_CheckedChanged($script:UpdateCount)
    $item.Controls.Add($cb, 0, 0)
    
    # So adiciona aos checkboxes controlaveis se NAO estiver instalado OU se for modo Cache
    if (-not $IsInstalled -or $script:CurrentMode -eq "ManageInstallers") {
        $script:Checkboxes += $cb
    }
    
    # Nome com indicador de instalado
    $displayName = if ($IsEssential -and -not $IsInstalled) { "$($App.N) *" } else { $App.N }
    if ($IsInstalled) { 
        $displayName += " (Instalado)" 
    } elseif ($ExtraInfo) { 
        $displayName += " ($ExtraInfo)" 
    }
    
    $lbl = New-Object System.Windows.Forms.Label
    $lbl.Text = $displayName
    # Cor do texto: cinza se instalado, verde se essencial, branco normal
    if ($IsInstalled -and $script:CurrentMode -ne "ManageInstallers") {
        $lbl.ForeColor = [System.Drawing.Color]::FromArgb(100, 100, 105)
    } elseif ($IsEssential) {
        $lbl.ForeColor = $Green
    } else {
        $lbl.ForeColor = $Text
    }
    $lbl.Font = New-Object System.Drawing.Font("Segoe UI", 9)
    $lbl.Dock = "Fill"
    $lbl.TextAlign = "MiddleLeft"
    $lbl.AutoSize = $false
    $item.Controls.Add($lbl, 1, 0)
    
    $catText = if ($App.C) { $App.C } else { $Source }
    $cat = New-Object System.Windows.Forms.Label
    $cat.Text = $catText
    $cat.ForeColor = if ($IsInstalled -and $script:CurrentMode -ne "ManageInstallers") { [System.Drawing.Color]::FromArgb(70, 70, 75) } else { $TextDim }
    $cat.Font = New-Object System.Drawing.Font("Segoe UI", 8)
    $cat.Dock = "Fill"
    $cat.TextAlign = "MiddleRight"
    $cat.AutoSize = $false
    $cat.Padding = New-Object System.Windows.Forms.Padding(0, 0, 8, 0)
    $item.Controls.Add($cat, 2, 0)
    
    # Eventos de clique apenas se nao estiver instalado OU se for modo Cache
    if (-not $IsInstalled -or $script:CurrentMode -eq "ManageInstallers") {
        $cbRef = $cb
        $item.Add_Click({ $cbRef.Checked = -not $cbRef.Checked }.GetNewClosure())
        $lbl.Add_Click({ $cbRef.Checked = -not $cbRef.Checked }.GetNewClosure())
        $cat.Add_Click({ $cbRef.Checked = -not $cbRef.Checked }.GetNewClosure())
    }
    
    $script:AppItems += $item
    return $item
}

function Test-AppAlreadyInstalled {
    param(
        [hashtable]$AppToCheck,
        [object[]]$InstalledList
    )
    
    if (-not $InstalledList -or $InstalledList.Count -eq 0) { return $false }
    
    $appId = if ($AppToCheck.I) { $AppToCheck.I.ToLower() } else { "" }
    $appName = if ($AppToCheck.N) { $AppToCheck.N.ToLower() } else { "" }
    
    foreach ($installed in $InstalledList) {
        $instId = if ($installed.I) { $installed.I.ToLower() } else { "" }
        $instName = if ($installed.N) { $installed.N.ToLower() } else { "" }
        
        if ($instId -eq $appId) { return $true }
        if ($appId -and $instId -like "*$appId*") { return $true }
        if ($instId.Length -gt 5 -and $appId -like "*$instId*") { return $true }
        if ($instName -eq $appName) { return $true }
        
        $appNameClean = $appName -replace '[^a-z0-9\s]', ''
        $instNameClean = $instName -replace '[^a-z0-9\s]', ''
        
        if ($appNameClean.Length -gt 3 -and $instNameClean -like "*$appNameClean*") { return $true }
        if ($instNameClean.Length -gt 3 -and $appNameClean -like "*$instNameClean*") { return $true }
    }
    
    return $false
}

# ============================================
# FUNCAO PARA CRIAR DIVISOR
# ============================================
function New-Divider {
    param([string]$Text)
    
    $divider = New-Object System.Windows.Forms.Label
    $divider.Text = "--- $Text ---"
    $divider.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 9)
    $divider.ForeColor = $TextDim
    $divider.Size = New-Object System.Drawing.Size(($ContentPanel.ClientSize.Width - 60), 30)
    $divider.TextAlign = "MiddleCenter"
    $divider.Margin = New-Object System.Windows.Forms.Padding(0, 10, 0, 5)
    return $divider
}

# ============================================
# FUNCAO SEARCH-WINGET-ONLINE
# ============================================
function Search-WingetOnline {
    param([string]$SearchTerm)
    
    if ([string]::IsNullOrWhiteSpace($SearchTerm) -or $SearchTerm.Length -lt 2) {
        Write-Log "Digite pelo menos 2 caracteres para buscar" -Type "Warning"
        return @()
    }
    
    Write-Log "Buscando '$SearchTerm' no repositorio winget..." -Type "Progress"
    Update-Stage "Searching"
    
    try {
        # Usar o modulo Microsoft.WinGet.Client se disponivel
        if (Initialize-WinGetModule) {
            Write-Log "Usando modulo WinGet para busca online..." -Type "Info"
            
            $results = Find-WinGetPackage -Query $SearchTerm -Count 50 -ErrorAction Stop
            $packages = @()
            
            foreach ($pkg in $results) {
                $packages += @{
                    N = $pkg.Name
                    I = $pkg.Id
                    Version = $pkg.Version
                    Source = $pkg.Source
                    Publisher = if ($pkg.Publisher) { $pkg.Publisher } else { "" }
                }
            }
            
            Update-Stage "Completed"
            Write-Log "Encontrados $($packages.Count) pacotes" -Type "Success"
            return $packages
        } else {
            # Fallback: usar winget search diretamente
            return Search-WingetOnlineFallback -SearchTerm $SearchTerm
        }
    } catch {
        Write-Log "Erro na busca online: $_" -Type "Warning"
        return Search-WingetOnlineFallback -SearchTerm $SearchTerm
    }
}

# Funcao fallback para busca online
function Search-WingetOnlineFallback {
    param([string]$SearchTerm)
    
    Write-Log "Usando metodo tradicional para busca..." -Type "Info"
    
    try {
        $originalOutputEncoding = [Console]::OutputEncoding
        [Console]::OutputEncoding = [System.Text.Utf8Encoding]::new()
        
        $output = & (Get-BananaSuisaWingetExe) search $SearchTerm --accept-source-agreements 2>&1 | Out-String
        $lines = $output -split "`n" | Where-Object { $_.Trim() -ne "" }
        
        [Console]::OutputEncoding = $originalOutputEncoding
        
        $packages = @()
        $dataStarted = $false
        
        foreach ($line in $lines) {
            # Ignorar linhas de progresso
            if ($line -match '^[\x00-\x1F\x7F-\xFF]' -or $line.Trim() -eq '') { continue }
            
            if ($line -match "^-+$") {
                $dataStarted = $true
                continue
            }
            
            if ($dataStarted -and $line.Trim()) {
                # Regex para capturar Name, Id, Version
                if ($line -match "^(.+?)\s{2,}(\S+)\s+(\S+)") {
                    $name = $matches[1].Trim()
                    $id = $matches[2].Trim()
                    $version = $matches[3].Trim()
                    
                    # Ignorar cabecalhos
                    if ($id -notmatch "^(Id|Identificador|Name|Nome)$" -and $id.Length -gt 2) {
                        $packages += @{
                            N = $name
                            I = $id
                            Version = $version
                            Source = "winget"
                        }
                    }
                }
            }
        }
        
        Update-Stage "Completed"
        Write-Log "Encontrados $($packages.Count) pacotes (fallback)" -Type "Success"
        return $packages
    } catch {
        Update-Stage "Failed"
        Write-Log "Erro na busca: $_" -Type "Error"
        return @()
    }
}

# ============================================
# FUNCAO GET-PENDING-WINDOWS-UPDATES
# ============================================
function Initialize-PSWindowsUpdate {
    # Verificar se o modulo PSWindowsUpdate esta disponivel
    if (-not (Get-Module -ListAvailable -Name PSWindowsUpdate)) {
        Write-Log "Instalando modulo PSWindowsUpdate..." -Type "Progress"
        try {
            # Garantir que NuGet provider esta instalado
            $nuget = Get-PackageProvider -Name NuGet -ErrorAction SilentlyContinue
            if (-not $nuget) {
                Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Force -Scope CurrentUser | Out-Null
            }
            
            Install-Module -Name PSWindowsUpdate -Force -Scope CurrentUser -AllowClobber -ErrorAction Stop
            Write-Log "Modulo PSWindowsUpdate instalado com sucesso" -Type "Success"
            return $true
        } catch {
            Write-Log "Erro ao instalar PSWindowsUpdate: $_" -Type "Error"
            return $false
        }
    }
    return $true
}

function Get-PendingWindowsUpdates {
    Write-Log "Buscando atualizacoes do Windows..." -Type "Progress"
    Update-Stage "Searching"
    
    try {
        if (-not (Initialize-PSWindowsUpdate)) {
            Update-Stage "Failed"
            return @()
        }
        
        Import-Module PSWindowsUpdate -ErrorAction Stop
        Write-Log "Verificando atualizacoes pendentes..." -Type "Info"
        
        $updates = Get-WindowsUpdate -ErrorAction Stop
        $result = @()
        
        foreach ($update in $updates) {
            $size = ""
            if ($update.Size) {
                $sizeMB = [math]::Round($update.Size / 1MB, 1)
                $size = "${sizeMB}MB"
            }
            
            $result += @{
                N = $update.Title
                I = $update.KB
                Version = $size
                Source = "WindowsUpdate"
                Category = if ($update.Categories) { $update.Categories[0].Name } else { "Update" }
                Update = $update
            }
        }
        
        Update-Stage "Completed"
        Write-Log "Encontradas $($result.Count) atualizacoes pendentes" -Type "Success"
        return $result
    } catch {
        Update-Stage "Failed"
        Write-Log "Erro ao buscar atualizacoes: $_" -Type "Error"
        return @()
    }
}

# ============================================
# SISTEMA DE DRIVERS - FUNCOES AUXILIARES
# ============================================

# Mapa de fabricantes conhecidos e suas paginas de drivers
$script:ManufacturerDriverPages = @{
    # Placas de video
    "NVIDIA"    = "https://www.nvidia.com/Download/index.aspx"
    "AMD"       = "https://www.amd.com/en/support"
    "ATI"       = "https://www.amd.com/en/support"
    "Intel"     = "https://www.intel.com/content/www/us/en/support/detect.html"
    
    # Fabricantes de PC
    "Dell"      = "https://www.dell.com/support/home"
    "HP"        = "https://support.hp.com/drivers"
    "Lenovo"    = "https://pcsupport.lenovo.com/br/pt/products/laptops-and-netbooks"
    "ASUS"      = "https://www.asus.com/support/Download-Center/"
    "Acer"      = "https://www.acer.com/ac/pt/BR/content/drivers"
    "Samsung"   = "https://www.samsung.com/br/support/"
    "MSI"       = "https://www.msi.com/support/download"
    "Gigabyte"  = "https://www.gigabyte.com/Support"
    
    # Perifericos
    "Realtek"   = "https://www.realtek.com/en/downloads"
    "Broadcom"  = "https://www.broadcom.com/support/download-search"
    "Qualcomm"  = "https://www.qualcomm.com/support"
    "Atheros"   = "https://www.qualcomm.com/support"
    "Synaptics" = "https://www.synaptics.com/products/touchpad-driver"
    "Logitech"  = "https://support.logi.com/hc/pt-br/articles/360025297893"
    "Razer"     = "https://www.razer.com/synapse-3"
    "Corsair"   = "https://www.corsair.com/br/pt/downloads"
    
    # Audio
    "Creative"  = "https://support.creative.com/Products/Products.aspx"
    "VIA"       = "https://www.viatech.com/en/support/drivers/"
    
    # Impressoras/Scanners
    "Canon"     = "https://www.usa.canon.com/support"
    "Epson"     = "https://epson.com/Support/sl/s"
    "Brother"   = "https://support.brother.com/g/b/productsearch.aspx"
    "Lexmark"   = "https://www.lexmark.com/pt_br/support/download-search.html"
    "Xerox"     = "https://www.support.xerox.com/"
    
    # USB/Chipset
    "Texas"     = "https://www.ti.com/support-software/drivers-software-702.html"
    
    # Rede
    "Killer"    = "https://support.killernetworking.com/"
    "Marvell"   = "https://www.marvell.com/support/downloads.html"
    "MediaTek"  = "https://www.mediatek.com/products/connectivity-and-networking"
}

# Extrair Hardware ID do dispositivo
function Get-HardwareInfo {
    param([string]$DeviceID)
    
    $info = @{
        VendorID = ""
        DeviceID = ""
        Manufacturer = ""
        SearchTerms = @()
    }
    
    try {
        # Extrair VEN_ e DEV_ do Device ID
        if ($DeviceID -match "VEN_([0-9A-F]{4})") {
            $info.VendorID = $Matches[1]
        }
        if ($DeviceID -match "DEV_([0-9A-F]{4})") {
            $info.DeviceID = $Matches[1]
        }
        
        # Extrair USB VID/PID
        if ($DeviceID -match "VID_([0-9A-F]{4})") {
            $info.VendorID = $Matches[1]
        }
        if ($DeviceID -match "PID_([0-9A-F]{4})") {
            $info.DeviceID = $Matches[1]
        }
        
        # Mapear Vendor IDs conhecidos para fabricantes
        $vendorMap = @{
            "10DE" = "NVIDIA"; "1002" = "AMD"; "8086" = "Intel"
            "1022" = "AMD"; "14E4" = "Broadcom"; "10EC" = "Realtek"
            "168C" = "Qualcomm"; "1969" = "Qualcomm"; "1B21" = "ASMedia"
            "1106" = "VIA"; "0BDA" = "Realtek"; "046D" = "Logitech"
            "1532" = "Razer"; "1B1C" = "Corsair"; "0DB0" = "Realtek"
            "04F2" = "Chicony"; "1BCF" = "Sunplus"; "17EF" = "Lenovo"
        }
        
        if ($info.VendorID -and $vendorMap.ContainsKey($info.VendorID.ToUpper())) {
            $info.Manufacturer = $vendorMap[$info.VendorID.ToUpper()]
        }
        
        # Termos de busca para pesquisa online
        if ($info.VendorID -and $info.DeviceID) {
            $info.SearchTerms += "VEN_$($info.VendorID) DEV_$($info.DeviceID) driver"
            $info.SearchTerms += "$($info.VendorID) $($info.DeviceID) driver download"
        }
        
    } catch {
        Write-Log "Erro ao extrair Hardware ID: $_" -Type "Warning"
    }
    
    return $info
}

# Identificar fabricante pelo nome do dispositivo
function Get-ManufacturerFromName {
    param([string]$DeviceName)
    
    $manufacturers = @(
        "NVIDIA", "AMD", "ATI", "Intel", "Realtek", "Broadcom", "Qualcomm", 
        "Atheros", "Synaptics", "Dell", "HP", "Lenovo", "ASUS", "Acer",
        "Samsung", "MSI", "Gigabyte", "Creative", "VIA", "Canon", "Epson",
        "Brother", "Logitech", "Razer", "Corsair", "ASMedia", "Marvell",
        "MediaTek", "Killer", "Conexant", "IDT", "Chicony", "Sunplus"
    )
    
    foreach ($mfr in $manufacturers) {
        if ($DeviceName -match $mfr) {
            return $mfr
        }
    }
    
    return $null
}

# Metodo 1: Windows Update via comando nativo (mais confiavel)
function Install-DriverViaWindowsUpdate {
    param($DriverItem)
    
    Write-Log "Tentando Windows Update nativo..." -Type "Info"
    
    try {
        # Usar UsoClient para forcar verificacao de updates (Windows 10+)
        $p = Start-Process -FilePath "UsoClient.exe" -ArgumentList "StartInteractiveScan" -PassThru -WindowStyle Hidden -ErrorAction SilentlyContinue
        if ($p) {
            while (-not $p.HasExited) {
                [System.Windows.Forms.Application]::DoEvents()
                Start-Sleep -Milliseconds 100
            }
        }
        Start-Sleep -Seconds 2
        
        # Verificar se o dispositivo foi resolvido
        if ($DriverItem.I) {
            $device = Get-CimInstance -ClassName Win32_PnPEntity | 
                Where-Object { $_.DeviceID -eq $DriverItem.I }
            
            if ($device -and $device.ConfigManagerErrorCode -eq 0) {
                return @{ Success = $true; Message = "Driver instalado via Windows Update" }
            }
        }
        
        return @{ Success = $false; Message = "Windows Update nao encontrou driver" }
    } catch {
        return @{ Success = $false; Message = "Erro no Windows Update: $_" }
    }
}

# Metodo 2: PnPUtil com scan forcado
function Install-DriverViaPnPUtil {
    param($DriverItem)
    
    Write-Log "Tentando PnPUtil scan..." -Type "Info"
    
    try {
        # Forcar enumeracao de dispositivos
        $null = & pnputil /scan-devices 2>&1
        Start-Sleep -Seconds 3
        
        # Tentar reinstalar o dispositivo
        if ($DriverItem.I) {
            # Desabilitar e reabilitar o dispositivo pode forcar reinstalacao do driver
            $deviceId = $DriverItem.I -replace '\\', '\\'
            
            # Usar devcon se disponivel, ou pnputil
            $null = & pnputil /enable-device "$deviceId" 2>&1
            Start-Sleep -Seconds 2
            
            # Verificar resultado
            $device = Get-CimInstance -ClassName Win32_PnPEntity | 
                Where-Object { $_.DeviceID -eq $DriverItem.I }
            
            if ($device -and $device.ConfigManagerErrorCode -eq 0) {
                return @{ Success = $true; Message = "Driver instalado via PnPUtil" }
            }
        }
        
        return @{ Success = $false; Message = "PnPUtil nao conseguiu resolver" }
    } catch {
        return @{ Success = $false; Message = "Erro no PnPUtil: $_" }
    }
}

# Metodo 3: DISM para buscar drivers online
function Install-DriverViaDISM {
    param($DriverItem)
    
    Write-Log "Tentando DISM online..." -Type "Info"
    
    try {
        # Usar DISM para verificar integridade e buscar drivers
        $null = & dism /online /cleanup-image /scanhealth 2>&1
        
        # Tentar restaurar componentes que podem incluir drivers
        $null = & dism /online /cleanup-image /restorehealth 2>&1
        Start-Sleep -Seconds 2
        
        # Verificar se resolveu
        if ($DriverItem.I) {
            $device = Get-CimInstance -ClassName Win32_PnPEntity | 
                Where-Object { $_.DeviceID -eq $DriverItem.I }
            
            if ($device -and $device.ConfigManagerErrorCode -eq 0) {
                return @{ Success = $true; Message = "Driver restaurado via DISM" }
            }
        }
        
        return @{ Success = $false; Message = "DISM nao conseguiu resolver" }
    } catch {
        return @{ Success = $false; Message = "Erro no DISM: $_" }
    }
}

# Metodo 4: PSWindowsUpdate (modulo PowerShell)
function Install-DriverViaPSWindowsUpdate {
    param($DriverItem)
    
    Write-Log "Tentando PSWindowsUpdate..." -Type "Info"
    
    try {
        # Se ja temos o objeto Update, usar diretamente
        if ($DriverItem.Update) {
            if (-not (Get-Module -Name PSWindowsUpdate -ErrorAction SilentlyContinue)) {
                Import-Module PSWindowsUpdate -ErrorAction Stop
            }
            
            $DriverItem.Update | Install-WindowsUpdate -AcceptAll -IgnoreReboot -ErrorAction Stop
            return @{ Success = $true; Message = "Driver instalado via PSWindowsUpdate" }
        }
        
        # Caso contrario, buscar driver especifico
        if (Initialize-PSWindowsUpdate) {
            Import-Module PSWindowsUpdate -ErrorAction SilentlyContinue
            
            # Buscar drivers disponiveis
            $drivers = Get-WindowsUpdate -Category "Drivers" -ErrorAction SilentlyContinue
            
            if ($drivers -and $drivers.Count -gt 0) {
                # Tentar encontrar driver relacionado ao dispositivo
                $deviceName = $DriverItem.N
                $matchingDriver = $drivers | Where-Object { 
                    $_.Title -match ($deviceName -replace '[^\w]', '.*') 
                } | Select-Object -First 1
                
                if ($matchingDriver) {
                    $matchingDriver | Install-WindowsUpdate -AcceptAll -IgnoreReboot -ErrorAction Stop
                    return @{ Success = $true; Message = "Driver instalado via PSWindowsUpdate" }
                }
            }
        }
        
        return @{ Success = $false; Message = "PSWindowsUpdate nao encontrou driver" }
    } catch {
        return @{ Success = $false; Message = "Erro no PSWindowsUpdate: $_" }
    }
}

# Metodo 5: Abrir pagina do fabricante (fallback final)
function Open-ManufacturerDriverPage {
    param($DriverItem)
    
    Write-Log "Buscando pagina do fabricante..." -Type "Info"
    
    $deviceName = if ($DriverItem.OriginalName) { $DriverItem.OriginalName } else { $DriverItem.N }
    $deviceId = $DriverItem.I
    $manufacturer = $DriverItem.Manufacturer
    $url = $null
    
    # Se nao temos fabricante, tentar identificar
    if (-not $manufacturer) {
        # Tentar identificar fabricante pelo nome
        $manufacturer = Get-ManufacturerFromName -DeviceName $deviceName
        
        # Se nao encontrou pelo nome, tentar pelo Hardware ID
        if (-not $manufacturer -and $deviceId) {
            $hwInfo = Get-HardwareInfo -DeviceID $deviceId
            if ($hwInfo.Manufacturer) {
                $manufacturer = $hwInfo.Manufacturer
            }
        }
    }
    
    # Se encontrou fabricante, usar pagina conhecida
    if ($manufacturer -and $script:ManufacturerDriverPages.ContainsKey($manufacturer)) {
        $url = $script:ManufacturerDriverPages[$manufacturer]
        Write-Log "Abrindo pagina de drivers: $manufacturer" -Type "Info"
    } else {
        # Fallback: buscar no Google/Bing
        $searchQuery = [System.Web.HttpUtility]::UrlEncode("$deviceName driver download Windows")
        $url = "https://www.google.com/search?q=$searchQuery"
        Write-Log "Abrindo busca online para: $deviceName" -Type "Info"
    }
    
    try {
        Start-Process $url
        
        $mfrMsg = if ($manufacturer) { " ($manufacturer)" } else { "" }
        return @{ 
            Success = $true
            Message = "Pagina do fabricante$mfrMsg aberta no navegador. Baixe e instale o driver manualmente."
            ManualAction = $true
        }
    } catch {
        return @{ 
            Success = $false
            Message = "Nao foi possivel abrir o navegador. Busque manualmente: $deviceName driver"
        }
    }
}

# Funcao principal melhorada para atualizar driver
function Update-DriverItemAdvanced {
    param($DriverItem)
    
    if ($script:CancelRequested) {
        return @{ Success = $false; ExitCode = -1; Message = "Cancelado" }
    }
    
    $driverName = $DriverItem.N
    Write-Log "Iniciando instalacao de driver: $driverName" -Type "Progress"
    Update-LogProgress "$driverName" "[Analisando...]"
    
    # Se e um driver do Windows Update com objeto Update, usar diretamente
    if ($DriverItem.Update) {
        Update-LogProgress "$driverName" "[Windows Update...]"
        $result = Install-DriverViaPSWindowsUpdate -DriverItem $DriverItem
        if ($result.Success) {
            Complete-LogProgress
            Write-Log "$driverName : $($result.Message)" -Type "Success"
            return @{ Success = $true; ExitCode = 0; Message = $result.Message }
        }
    }
    
    # Array de metodos para tentar em ordem
    $methods = @(
        @{ Name = "Windows Update"; Status = "[Windows Update...]"; Func = { Install-DriverViaWindowsUpdate -DriverItem $DriverItem } },
        @{ Name = "PnPUtil"; Status = "[PnPUtil...]"; Func = { Install-DriverViaPnPUtil -DriverItem $DriverItem } },
        @{ Name = "PSWindowsUpdate"; Status = "[PSWindowsUpdate...]"; Func = { Install-DriverViaPSWindowsUpdate -DriverItem $DriverItem } }
    )
    
    # Tentar cada metodo automatico
    foreach ($method in $methods) {
        if ($script:CancelRequested) {
            Complete-LogProgress
            return @{ Success = $false; ExitCode = -1; Message = "Cancelado" }
        }
        
        Update-LogProgress "$driverName" $method.Status
        Write-Log "Metodo: $($method.Name)" -Type "Info"
        
        try {
            $result = & $method.Func
            
            if ($result.Success -and -not $result.ManualAction) {
                Complete-LogProgress
                Write-Log "$driverName : $($result.Message)" -Type "Success"
                return @{ Success = $true; ExitCode = 0; Message = $result.Message }
            }
        } catch {
            Write-Log "Erro no metodo $($method.Name): $_" -Type "Warning"
        }
        
        # Pequena pausa entre tentativas
        Start-Sleep -Milliseconds 500
    }
    
    # Fallback: Abrir pagina do fabricante
    Update-LogProgress "$driverName" "[Buscando fabricante...]"
    $fallbackResult = Open-ManufacturerDriverPage -DriverItem $DriverItem
    
    Complete-LogProgress
    
    if ($fallbackResult.Success) {
        Write-Log "$driverName : $($fallbackResult.Message)" -Type "Warning"
        return @{ 
            Success = $true
            ExitCode = 0
            Message = $fallbackResult.Message
            ManualAction = $true
        }
    }
    
    Write-Log "$driverName : Nao foi possivel instalar automaticamente" -Type "Error"
    return @{ 
        Success = $false
        ExitCode = -1
        Message = "Nao foi possivel instalar o driver. Busque manualmente: $driverName"
    }
}

# ============================================
# FUNCAO GET-MISSING-DRIVERS
# ============================================
function Get-MissingDrivers {
    Write-Log "Buscando dispositivos com problemas de driver..." -Type "Progress"
    Update-Stage "Searching"
    
    try {
        # Buscar dispositivos com problema via WMI
        # ConfigManagerErrorCode != 0 indica problema
        $devices = Get-CimInstance -ClassName Win32_PnPEntity -ErrorAction Stop | 
            Where-Object { $_.ConfigManagerErrorCode -ne 0 }
        
        $result = @()
        
        foreach ($device in $devices) {
            # Traduzir codigo de erro
            $errorMsg = switch ($device.ConfigManagerErrorCode) {
                1 { "Nao configurado corretamente" }
                3 { "Driver corrompido" }
                10 { "Nao pode iniciar" }
                12 { "Recursos insuficientes" }
                14 { "Requer reinicializacao" }
                18 { "Reinstalar drivers" }
                19 { "Registro corrompido" }
                21 { "Windows esta removendo" }
                22 { "Dispositivo desabilitado" }
                24 { "Nao presente/nao funciona" }
                28 { "Drivers nao instalados" }
                29 { "Recurso desabilitado no firmware" }
                31 { "Windows nao consegue carregar drivers" }
                32 { "Driver desabilitado" }
                33 { "Recurso nao determinado" }
                34 { "Configuracao manual necessaria" }
                35 { "Firmware incompleto" }
                36 { "IRQ em conflito" }
                37 { "Driver nao inicializa" }
                38 { "Driver ja carregado" }
                39 { "Registro corrompido" }
                40 { "Servico nao encontrado" }
                41 { "Hardware duplicado" }
                42 { "Driver duplicado" }
                43 { "Servico de enumeracao falhou" }
                44 { "Reinicializacao pendente" }
                45 { "Dispositivo nao conectado" }
                46 { "Acesso negado ao dispositivo" }
                47 { "Preparado para remocao" }
                48 { "Driver bloqueado" }
                49 { "Registro muito grande" }
                50 { "Chave do registro excluida" }
                51 { "Falha preparando hardware" }
                52 { "Verificacao de assinatura falhou" }
                default { "Erro desconhecido (Cod: $($device.ConfigManagerErrorCode))" }
            }
            
            # Tentar identificar fabricante
            $deviceName = if ($device.Name) { $device.Name } else { "Dispositivo Desconhecido" }
            $manufacturer = Get-ManufacturerFromName -DeviceName $deviceName
            
            # Se nao encontrou pelo nome, tentar pelo Hardware ID
            if (-not $manufacturer -and $device.DeviceID) {
                $hwInfo = Get-HardwareInfo -DeviceID $device.DeviceID
                if ($hwInfo.Manufacturer) {
                    $manufacturer = $hwInfo.Manufacturer
                }
            }
            
            # Adicionar fabricante ao nome se encontrado
            $displayName = $deviceName
            if ($manufacturer -and $deviceName -notmatch $manufacturer) {
                $displayName = "[$manufacturer] $deviceName"
            }
            
            $result += @{
                N = $displayName
                I = $device.DeviceID
                Version = $errorMsg
                Source = "Driver"
                Category = if ($device.PNPClass) { $device.PNPClass } else { "Unknown" }
                Status = $device.Status
                ErrorCode = $device.ConfigManagerErrorCode
                Manufacturer = $manufacturer
                OriginalName = $deviceName
            }
        }
        
        # Tambem buscar drivers disponiveis via Windows Update
        if (Initialize-PSWindowsUpdate) {
            try {
                Import-Module PSWindowsUpdate -ErrorAction SilentlyContinue
                $driverUpdates = Get-WindowsUpdate -Category "Drivers" -ErrorAction SilentlyContinue
                
                foreach ($driver in $driverUpdates) {
                    $size = ""
                    if ($driver.Size) {
                        $sizeMB = [math]::Round($driver.Size / 1MB, 1)
                        $size = "${sizeMB}MB"
                    }
                    
                    $result += @{
                        N = $driver.Title
                        I = $driver.KB
                        Version = $size
                        Source = "DriverUpdate"
                        Category = "Driver Disponivel"
                        Update = $driver
                        ErrorCode = 0
                    }
                }
            } catch {
                # Ignorar erros ao buscar drivers via Windows Update
            }
        }
        
        Update-Stage "Completed"
        $problemCount = ($result | Where-Object { $_.ErrorCode -ne 0 }).Count
        $availableCount = ($result | Where-Object { $_.ErrorCode -eq 0 }).Count
        Write-Log "Encontrados $problemCount dispositivo(s) com problema, $availableCount driver(s) disponiveis" -Type "Success"
        return $result
    } catch {
        Update-Stage "Failed"
        Write-Log "Erro ao buscar drivers: $_" -Type "Error"
        return @()
    }
}

# ============================================
# FUNCAO GET-WINGET-UPDATES
# ============================================
function Get-WingetUpdates {
    Write-Log "Buscando atualizacoes disponiveis..." -Type "Progress"
    Update-Stage "Searching"
    
    try {
        # Tentar usar o modulo Microsoft.WinGet.Client primeiro
        if (Initialize-WinGetModule) {
            Write-Log "Usando modulo WinGet para buscar atualizacoes..." -Type "Info"
            
            $packages = Get-WinGetPackage -ErrorAction Stop | Where-Object { 
                $_.IsUpdateAvailable -eq $true 
            }
            
            $updates = @()
            foreach ($pkg in $packages) {
                $newVersion = if ($pkg.AvailableVersions -and $pkg.AvailableVersions.Count -gt 0) {
                    $pkg.AvailableVersions[0]
                } else {
                    "Disponivel"
                }
                
                $updates += @{
                    N = $pkg.Name
                    I = $pkg.Id
                    CurrentVersion = $pkg.InstalledVersion
                    NewVersion = $newVersion
                    Source = $pkg.Source
                }
            }
            
            Update-Stage "Completed"
            Write-Log "Encontradas $($updates.Count) atualizacoes" -Type "Success"
            return $updates
        } else {
            # Fallback: parsing tradicional
            return Get-WingetUpdatesFallback
        }
    } catch {
        Write-Log "Erro com modulo WinGet, usando fallback: $_" -Type "Warning"
        return Get-WingetUpdatesFallback
    }
}

# Funcao fallback para sistemas sem o modulo
function Get-WingetUpdatesFallback {
    Write-Log "Usando metodo tradicional para buscar atualizacoes..." -Type "Info"
    Update-Stage "Searching"
    
    try {
        # Corrigir encoding UTF-8 para parsing correto
        $originalOutputEncoding = [Console]::OutputEncoding
        [Console]::OutputEncoding = [System.Text.Utf8Encoding]::new()
        
        $output = & (Get-BananaSuisaWingetExe) upgrade --accept-source-agreements 2>&1 | Out-String
        $lines = $output -split "`n" | Where-Object { $_.Trim() -ne "" }
        
        # Restaurar encoding
        [Console]::OutputEncoding = $originalOutputEncoding
        
        $updates = @()
        $dataStarted = $false
        
        foreach ($line in $lines) {
            # Ignorar linhas de progresso
            # Ignorar linhas de progresso (caracteres de controle ou barras de progresso)
            if ($line -match '^[\x00-\x1F\x7F-\xFF]' -or $line.Trim() -eq '' -or $line -match '^\s*[\u2580-\u259F]') { continue }
            
            if ($line -match "^-+$") {
                $dataStarted = $true
                continue
            }
            if ($line -match "^\d+ upgrades available" -or $line -match "atualizac") {
                continue
            }
            if ($dataStarted -and $line.Trim()) {
                # Parse: Name Id Version Available Source
                if ($line -match "^(.+?)\s{2,}(\S+)\s+(\S+)\s+(\S+)\s*(\S*)") {
                    $name = $matches[1].Trim()
                    $id = $matches[2].Trim()
                    
                    # Ignorar linhas de cabecalho
                    if ($id -notmatch "^(Id|Identificador|Name|Nome)$" -and $id.Length -gt 2) {
                        $updates += @{
                            N = $name
                            I = $id
                            CurrentVersion = $matches[3].Trim()
                            NewVersion = $matches[4].Trim()
                            Source = if ($matches[5]) { $matches[5].Trim() } else { "winget" }
                        }
                    }
                }
            }
        }
        
        Update-Stage "Completed"
        Write-Log "Encontradas $($updates.Count) atualizacoes (fallback)" -Type "Success"
        return $updates
    } catch {
        Update-Stage "Failed"
        Write-Log "Erro ao buscar atualizacoes: $_" -Type "Error"
        return @()
    }
}

# ============================================
# FUNCAO GET-INSTALLED-APPS
# ============================================
function Get-InstalledApps {
    Write-Log "Buscando apps instalados..." -Type "Progress"
    Update-Stage "Searching"
    
    try {
        # Tentar usar o modulo Microsoft.WinGet.Client primeiro
        if (Initialize-WinGetModule) {
            Write-Log "Usando modulo WinGet para listar apps..." -Type "Info"
            
            $packages = Get-WinGetPackage -ErrorAction Stop
            $installed = @()
            
            foreach ($pkg in $packages) {
                # Determinar categoria baseado na fonte
                $source = if ($pkg.Source) { $pkg.Source.ToLower() } else { "unknown" }
                $category = "Winget"
                
                if ($source -eq "msstore" -or $source -eq "microsoft store") {
                    $category = "Windows Apps"
                } elseif ($source -eq "winget") {
                    $category = "Winget"
                } elseif (-not $pkg.Source -or $source -eq "unknown") {
                    # Apps sem fonte definida geralmente sao instalados localmente
                    $category = "Outros"
                }
                
                $installed += @{
                    N = $pkg.Name
                    I = $pkg.Id
                    Version = $pkg.InstalledVersion
                    Source = $pkg.Source
                    Category = $category
                }
            }
            
            Update-Stage "Completed"
            Write-Log "Encontrados $($installed.Count) apps instalados" -Type "Success"
            return $installed
        } else {
            # Fallback: parsing tradicional com encoding corrigido
            return Get-InstalledAppsFallback
        }
    } catch {
        Write-Log "Erro com modulo WinGet, usando fallback: $_" -Type "Warning"
        return Get-InstalledAppsFallback
    }
}

# ============================================
# FUNCAO GET-ALL-INSTALLED-APPS (Categorizado)
# ============================================
function Get-AllInstalledAppsCategorized {
    Write-Log "Buscando todos os apps instalados..." -Type "Progress"
    Update-Stage "Searching"
    
    $allApps = @{
        Winget = @()
        Store = @()
        UWP = @()
        Local = @()
    }
    
    # Listas para controle de duplicatas
    $processedNames = @{}
    
    try {
        # =============================================
        # ETAPA 1: Buscar do REGISTRO (fonte primaria para EXE/MSI)
        # =============================================
        Write-Log "Buscando programas instalados (Registro)..." -Type "Info"
        $registryPaths = @(
            "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*",
            "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*",
            "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*"
        )
        
        $registryApps = @()
        
        foreach ($path in $registryPaths) {
            try {
                $regItems = Get-ItemProperty $path -ErrorAction SilentlyContinue | 
                    Where-Object { $_.DisplayName -and $_.DisplayName.Trim() -ne "" }
                
                foreach ($regApp in $regItems) {
                    $name = $regApp.DisplayName.Trim()
                    $nameLower = $name.ToLower()
                    
                    # Pular duplicatas
                    if ($processedNames.ContainsKey($nameLower)) { continue }
                    
                    # Pular apps do sistema/framework
                    if ($name -match "^(Microsoft \.NET|Microsoft Visual C\+\+|Windows SDK|VS \d|Update for|Security Update|Hotfix)") { continue }
                    if ($name -match "^(KB\d+|\.NET Framework|Microsoft Windows Desktop Runtime)") { continue }
                    if ($name -match "^(Windows Driver|NVIDIA Graphics Driver|AMD Software)") { continue }
                    
                    $version = if ($regApp.DisplayVersion) { $regApp.DisplayVersion } else { "" }
                    $publisher = if ($regApp.Publisher) { $regApp.Publisher } else { "" }
                    
                    # Gerar ID baseado no nome
                    $id = "Local." + ($name -replace '[^a-zA-Z0-9]', '')
                    
                    $appData = @{
                        N = $name
                        I = $id
                        Version = $version
                        Source = "Local"
                        Publisher = $publisher
                        UninstallString = $regApp.UninstallString
                        RegistryKey = $regApp.PSPath
                    }
                    
                    $registryApps += $appData
                    $processedNames[$nameLower] = $true
                }
            } catch {
                # Ignorar erros de acesso ao registro
            }
        }
        
        # =============================================
        # ETAPA 2: Buscar via WINGET (apenas Source definido)
        # =============================================
        if (Initialize-WinGetModule) {
            Write-Log "Buscando apps do Winget e Store..." -Type "Info"
            $packages = Get-WinGetPackage -ErrorAction Stop
            
            foreach ($pkg in $packages) {
                $source = if ($pkg.Source) { $pkg.Source.ToLower() } else { "" }
                $nameLower = $pkg.Name.ToLower()
                
                # Apenas processar se tem Source definido (winget ou msstore)
                if ($source -eq "winget") {
                    # App instalado via winget
                    $appData = @{
                        N = $pkg.Name
                        I = $pkg.Id
                        Version = $pkg.InstalledVersion
                        Source = "winget"
                    }
                    $allApps.Winget += $appData
                    
                    # Remover do registro se existir (evitar duplicata)
                    $registryApps = @($registryApps | Where-Object { 
                        $regName = $_.N.ToLower()
                        -not ($regName -eq $nameLower -or $regName -like "*$nameLower*" -or $nameLower -like "*$regName*")
                    })
                    
                } elseif ($source -eq "msstore") {
                    # App da Microsoft Store
                    $appData = @{
                        N = $pkg.Name
                        I = $pkg.Id
                        Version = $pkg.InstalledVersion
                        Source = "msstore"
                    }
                    $allApps.Store += $appData
                    
                    # Remover do registro se existir
                    $registryApps = @($registryApps | Where-Object { 
                        $regName = $_.N.ToLower()
                        -not ($regName -eq $nameLower -or $regName -like "*$nameLower*" -or $nameLower -like "*$regName*")
                    })
                }
                # Apps com Source vazio sao ignorados aqui (ja estao no registro)
            }
        }
        
        # =============================================
        # ETAPA 3: Buscar WINDOWS APPS (UWP/MSIX)
        # =============================================
        # Buscar UWP apenas se AppX estiver disponivel (evita crash em Windows modificados)
        if ($script:AppXAvailable) {
            Write-Log "Buscando Windows Apps (UWP)..." -Type "Info"
        } else {
            Write-Log "Pulando UWP (AppX indisponivel)..." -Type "Warning"
        }
        
        if ($script:AppXAvailable) {
            try {
                # Obter nomes ja processados
                $wingetNames = $allApps.Winget | ForEach-Object { $_.N.ToLower() }
                $storeNames = $allApps.Store | ForEach-Object { $_.N.ToLower() }
            
            $uwpApps = Get-AppxPackage -ErrorAction SilentlyContinue | 
                Where-Object { $_.IsFramework -eq $false -and $_.SignatureKind -ne "System" }
            
            foreach ($uwp in $uwpApps) {
                # Tentar obter nome amigavel
                $name = $uwp.Name
                if ($uwp.Name -match "\.") { 
                    $name = ($uwp.Name -split "\.")[-1] 
                }
                
                # Usar DisplayName se disponivel e nao for resource
                try {
                    $manifest = Get-AppxPackageManifest $uwp -ErrorAction SilentlyContinue
                    if ($manifest -and $manifest.Package.Properties.DisplayName -and 
                        $manifest.Package.Properties.DisplayName -notmatch "^ms-resource:") {
                        $name = $manifest.Package.Properties.DisplayName
                    }
                } catch { }
                
                $nameLower = $name.ToLower()
                
                # Pular se ja existe no Winget ou Store
                $isDuplicate = $false
                foreach ($existName in ($wingetNames + $storeNames)) {
                    if ($nameLower -eq $existName -or 
                        ($nameLower -like "*$existName*" -and $existName.Length -gt 3) -or 
                        ($existName -like "*$nameLower*" -and $nameLower.Length -gt 3)) {
                        $isDuplicate = $true
                        break
                    }
                }
                if ($isDuplicate) { continue }
                
                # Pular apps do sistema Microsoft
                if ($uwp.Publisher -match "CN=Microsoft" -and $name -match "^(Microsoft\.|Windows\.|MicrosoftWindows|InputApp|Extension|VP9|HEIF|WebMediaExtensions|LanguageExperiencePack)") {
                    continue
                }
                
                # Pular se nome parece ser um GUID ou muito tecnico
                if ($name -match "^[a-f0-9]{8}-" -or $name -match "^\d+\.\d+\.\d+") { continue }
                
                $appData = @{
                    N = $name
                    I = $uwp.PackageFamilyName
                    Version = $uwp.Version.ToString()
                    Source = "UWP"
                    Publisher = $uwp.Publisher
                }
                
                $allApps.UWP += $appData
                
                # Remover do registro se existir
                $registryApps = @($registryApps | Where-Object { 
                    $regName = $_.N.ToLower()
                    -not ($regName -eq $nameLower -or 
                          ($regName -like "*$nameLower*" -and $nameLower.Length -gt 3) -or 
                          ($nameLower -like "*$regName*" -and $regName.Length -gt 3))
                })
            }
            } catch {
                Write-Log "Erro ao buscar UWP: $_" -Type "Warning"
            }
        } # Fim do if AppXAvailable
        
        # =============================================
        # ETAPA 4: Adicionar apps do REGISTRO restantes
        # =============================================
        $allApps.Local = $registryApps
        
        # Ordenar cada categoria por nome
        $allApps.Winget = @($allApps.Winget | Sort-Object { $_.N })
        $allApps.Store = @($allApps.Store | Sort-Object { $_.N })
        $allApps.UWP = @($allApps.UWP | Sort-Object { $_.N })
        $allApps.Local = @($allApps.Local | Sort-Object { $_.N })
        
        $total = $allApps.Winget.Count + $allApps.Store.Count + $allApps.UWP.Count + $allApps.Local.Count
        Update-Stage "Completed"
        Write-Log "Encontrados $total apps (Winget: $($allApps.Winget.Count), Store: $($allApps.Store.Count), UWP: $($allApps.UWP.Count), Local: $($allApps.Local.Count))" -Type "Success"
        
        return $allApps
    } catch {
        Update-Stage "Failed"
        Write-Log "Erro ao buscar apps: $_" -Type "Error"
        return $allApps
    }
}

# Funcao fallback para sistemas sem o modulo
function Get-InstalledAppsFallback {
    Write-Log "Usando metodo tradicional para listar apps..." -Type "Info"
    Update-Stage "Searching"
    
    try {
        # Corrigir encoding UTF-8 para parsing correto
        $originalOutputEncoding = [Console]::OutputEncoding
        [Console]::OutputEncoding = [System.Text.Utf8Encoding]::new()
        
        $output = & (Get-BananaSuisaWingetExe) list --accept-source-agreements 2>&1 | Out-String
        $lines = $output -split "`n" | Where-Object { $_.Trim() -ne "" }
        
        # Restaurar encoding
        [Console]::OutputEncoding = $originalOutputEncoding
        
        $installed = @()
        $dataStarted = $false
        
        foreach ($line in $lines) {
            # Ignorar linhas de progresso
            # Ignorar linhas de progresso (caracteres de controle ou barras de progresso)
            if ($line -match '^[\x00-\x1F\x7F-\xFF]' -or $line.Trim() -eq '' -or $line -match '^\s*[\u2580-\u259F]') { continue }
            
            if ($line -match "^-+$") {
                $dataStarted = $true
                continue
            }
            if ($dataStarted -and $line.Trim()) {
                # Regex melhorado para capturar Name, Id, Version
                if ($line -match "^(.+?)\s{2,}(\S+)\s+(\S+)") {
                    $name = $matches[1].Trim()
                    $id = $matches[2].Trim()
                    $version = $matches[3].Trim()
                    
                    # Ignorar linhas que sao claramente cabecalhos ou invalidas
                    if ($id -notmatch "^(Id|Identificador|Name|Nome)$" -and $id.Length -gt 2) {
                        $installed += @{
                            N = $name
                            I = $id
                            Version = $version
                        }
                    }
                }
            }
        }
        
        Update-Stage "Completed"
        Write-Log "Encontrados $($installed.Count) apps instalados (fallback)" -Type "Success"
        return $installed
    } catch {
        Update-Stage "Failed"
        Write-Log "Erro ao listar apps: $_" -Type "Error"
        return @()
    }
}

#endregion
