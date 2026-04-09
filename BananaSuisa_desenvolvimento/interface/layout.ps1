#region [07-UI-LAYOUT] Header, sidebar, conteudo e footer

# ============================================
# FOOTER - Log expansivel e botoes de acao
# ============================================
$Footer = New-Object System.Windows.Forms.Panel
$Footer.Dock = "Bottom"
$Footer.Height = 76
$Footer.BackColor = $Panel
$Form.Controls.Add($Footer)

# Log expansivel (ocupa todo espaco disponivel)
$LogBox = New-Object System.Windows.Forms.RichTextBox
$LogBox.Location = New-Object System.Drawing.Point(15, 5)
$LogBox.Size = New-Object System.Drawing.Size(400, 50)
$LogBox.BackColor = $LogBG
$LogBox.ForeColor = $TextDim
$LogBox.Font = New-Object System.Drawing.Font("Consolas", 8)
$LogBox.BorderStyle = "None"
$LogBox.ReadOnly = $true
$LogBox.ScrollBars = "ForcedVertical"
$LogBox.WordWrap = $false
$LogBox.DetectUrls = $false
$Footer.Controls.Add($LogBox)

# Label de etapa (dentro do log, sera reposicionada)
$StageLabel = New-Object System.Windows.Forms.Label
$StageLabel.Text = ""
$StageLabel.ForeColor = $Blue
$StageLabel.Font = New-Object System.Drawing.Font("Segoe UI", 8)
$StageLabel.AutoSize = $true
$StageLabel.Visible = $false
$Footer.Controls.Add($StageLabel)

# Contador (acima do botao de acao)
$LblCount = New-Object System.Windows.Forms.Label
$LblCount.Text = "0 selecionados"
$LblCount.ForeColor = $TextDim
$LblCount.Font = New-Object System.Drawing.Font("Segoe UI", 9)
$LblCount.AutoSize = $true
$LblCount.Visible = $false
$Footer.Controls.Add($LblCount)

# Botao de acao principal (unico botao visivel)
$BtnAction = New-Object System.Windows.Forms.Button
$BtnAction.Text = "EXECUTAR"
$BtnAction.Size = New-Object System.Drawing.Size(120, 40)
$BtnAction.FlatStyle = "Flat"
$BtnAction.FlatAppearance.BorderSize = 0
$BtnAction.BackColor = $Accent
$BtnAction.ForeColor = $Text
$BtnAction.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 10)
$BtnAction.Cursor = "Hand"
$BtnAction.Visible = $false
$Footer.Controls.Add($BtnAction)

# ============================================
# SIDEBAR - Posicionamento manual (15% da largura)
# ============================================
$SidebarPanel = New-Object System.Windows.Forms.Panel
$SidebarPanel.BackColor = $Sidebar
$Form.Controls.Add($SidebarPanel)

$SidebarSectionPrimary = New-Object System.Windows.Forms.Label
$SidebarSectionPrimary.Text = "Navegacao"
$SidebarSectionPrimary.ForeColor = $TextDim
$SidebarSectionPrimary.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 8.5)
$SidebarSectionPrimary.AutoSize = $true
$SidebarPanel.Controls.Add($SidebarSectionPrimary)

$SidebarSectionContext = New-Object System.Windows.Forms.Label
$SidebarSectionContext.Text = "Acoes do modo"
$SidebarSectionContext.ForeColor = $TextDim
$SidebarSectionContext.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 8.5)
$SidebarSectionContext.AutoSize = $true
$SidebarSectionContext.Visible = $false
$SidebarPanel.Controls.Add($SidebarSectionContext)

$SidebarSectionUtility = New-Object System.Windows.Forms.Label
$SidebarSectionUtility.Text = "Ferramentas"
$SidebarSectionUtility.ForeColor = $TextDim
$SidebarSectionUtility.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 8.5)
$SidebarSectionUtility.AutoSize = $true
$SidebarPanel.Controls.Add($SidebarSectionUtility)

# --- ESTADO 1: Menu Principal ---
$BtnModeInstall = New-Object System.Windows.Forms.Button
$BtnModeInstall.Text = "INSTALAR"
$BtnModeInstall.Size = New-Object System.Drawing.Size(120, 32)
$BtnModeInstall.Location = New-Object System.Drawing.Point(10, 15)
$BtnModeInstall.FlatStyle = "Flat"
$BtnModeInstall.FlatAppearance.BorderSize = 0
$BtnModeInstall.BackColor = $SidebarBtn
$BtnModeInstall.ForeColor = $Green
$BtnModeInstall.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 9)
$BtnModeInstall.Cursor = "Hand"
$BtnModeInstall.TextAlign = "MiddleCenter"
$SidebarPanel.Controls.Add($BtnModeInstall)

$BtnModeUpdate = New-Object System.Windows.Forms.Button
$BtnModeUpdate.Text = "ATUALIZAR"
$BtnModeUpdate.Size = New-Object System.Drawing.Size(120, 32)
$BtnModeUpdate.Location = New-Object System.Drawing.Point(10, 55)
$BtnModeUpdate.FlatStyle = "Flat"
$BtnModeUpdate.FlatAppearance.BorderSize = 0
$BtnModeUpdate.BackColor = $SidebarBtn
$BtnModeUpdate.ForeColor = $Blue
$BtnModeUpdate.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 9)
$BtnModeUpdate.Cursor = "Hand"
$BtnModeUpdate.TextAlign = "MiddleCenter"
$SidebarPanel.Controls.Add($BtnModeUpdate)

$BtnModeRemove = New-Object System.Windows.Forms.Button
$BtnModeRemove.Text = "REMOVER"
$BtnModeRemove.Size = New-Object System.Drawing.Size(120, 32)
$BtnModeRemove.Location = New-Object System.Drawing.Point(10, 95)
$BtnModeRemove.FlatStyle = "Flat"
$BtnModeRemove.FlatAppearance.BorderSize = 0
$BtnModeRemove.BackColor = $SidebarBtn
$BtnModeRemove.ForeColor = $Red
$BtnModeRemove.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 9)
$BtnModeRemove.Cursor = "Hand"
$BtnModeRemove.TextAlign = "MiddleCenter"
$SidebarPanel.Controls.Add($BtnModeRemove)

$BtnModeSystem = New-Object System.Windows.Forms.Button
$BtnModeSystem.Text = "SISTEMA"
$BtnModeSystem.Size = New-Object System.Drawing.Size(120, 32)
$BtnModeSystem.Location = New-Object System.Drawing.Point(10, 135)
$BtnModeSystem.FlatStyle = "Flat"
$BtnModeSystem.FlatAppearance.BorderSize = 0
$BtnModeSystem.BackColor = $SidebarBtn
$BtnModeSystem.ForeColor = [System.Drawing.Color]::FromArgb(255, 180, 100)  # Laranja
$BtnModeSystem.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 9)
$BtnModeSystem.Cursor = "Hand"
$BtnModeSystem.TextAlign = "MiddleCenter"
$SidebarPanel.Controls.Add($BtnModeSystem)

$BtnModePrinters = New-Object System.Windows.Forms.Button
$BtnModePrinters.Text = "IMPRESSORAS"
$BtnModePrinters.Size = New-Object System.Drawing.Size(120, 32)
$BtnModePrinters.Location = New-Object System.Drawing.Point(10, 175)
$BtnModePrinters.FlatStyle = "Flat"
$BtnModePrinters.FlatAppearance.BorderSize = 0
$BtnModePrinters.BackColor = $SidebarBtn
$BtnModePrinters.ForeColor = [System.Drawing.Color]::FromArgb(100, 181, 246)  # Azul claro
$BtnModePrinters.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 9)
$BtnModePrinters.Cursor = "Hand"
$BtnModePrinters.TextAlign = "MiddleCenter"
$SidebarPanel.Controls.Add($BtnModePrinters)

$BtnModeStorage = New-Object System.Windows.Forms.Button
$BtnModeStorage.Text = "INSTALADORES"
$BtnModeStorage.Size = New-Object System.Drawing.Size(120, 32)
$BtnModeStorage.Location = New-Object System.Drawing.Point(10, 215)
$BtnModeStorage.FlatStyle = "Flat"
$BtnModeStorage.FlatAppearance.BorderSize = 0
$BtnModeStorage.BackColor = $SidebarBtn
$BtnModeStorage.ForeColor = [System.Drawing.Color]::FromArgb(144, 202, 249) # Light Blue
$BtnModeStorage.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 9)
$BtnModeStorage.Cursor = "Hand"
$BtnModeStorage.TextAlign = "MiddleCenter"
$SidebarPanel.Controls.Add($BtnModeStorage)

# --- ESTADO 2: Modo Ativo (inicialmente ocultos) ---
$BtnBack = New-Object System.Windows.Forms.Button
$BtnBack.Text = "< Voltar"
$BtnBack.Size = New-Object System.Drawing.Size(120, 35)
$BtnBack.Location = New-Object System.Drawing.Point(10, 10)
$BtnBack.FlatStyle = "Flat"
$BtnBack.FlatAppearance.BorderColor = $TextDim
$BtnBack.BackColor = $Sidebar
$BtnBack.ForeColor = $TextDim
$BtnBack.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 9)
$BtnBack.TextAlign = "MiddleCenter"
$BtnBack.Cursor = "Hand"
$BtnBack.Visible = $false
$SidebarPanel.Controls.Add($BtnBack)

$BtnAll = New-Object System.Windows.Forms.Button
$BtnAll.Text = "Todos"
$BtnAll.Size = New-Object System.Drawing.Size(120, 32)
$BtnAll.Location = New-Object System.Drawing.Point(10, 55)
$BtnAll.FlatStyle = "Flat"
$BtnAll.FlatAppearance.BorderColor = $TextDim
$BtnAll.BackColor = $SidebarBtn
$BtnAll.ForeColor = $TextDim
$BtnAll.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 9)
$BtnAll.TextAlign = "MiddleCenter"
$BtnAll.Cursor = "Hand"
$BtnAll.Visible = $false
$SidebarPanel.Controls.Add($BtnAll)

$BtnNone = New-Object System.Windows.Forms.Button
$BtnNone.Text = "Limpar"
$BtnNone.Size = New-Object System.Drawing.Size(120, 32)
$BtnNone.Location = New-Object System.Drawing.Point(10, 95)
$BtnNone.FlatStyle = "Flat"
$BtnNone.FlatAppearance.BorderColor = $TextDim
$BtnNone.BackColor = $SidebarBtn
$BtnNone.ForeColor = $TextDim
$BtnNone.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 9)
$BtnNone.TextAlign = "MiddleCenter"
$BtnNone.Cursor = "Hand"
$BtnNone.Visible = $false
$SidebarPanel.Controls.Add($BtnNone)

# Botao Buscar Online (apenas modo Instalar)
$BtnSearchOnline = New-Object System.Windows.Forms.Button
$BtnSearchOnline.Text = "Buscar Online"
$BtnSearchOnline.Size = New-Object System.Drawing.Size(120, 32)
$BtnSearchOnline.Location = New-Object System.Drawing.Point(10, 275)
$BtnSearchOnline.FlatStyle = "Flat"
$BtnSearchOnline.FlatAppearance.BorderColor = $Blue
$BtnSearchOnline.BackColor = [System.Drawing.Color]::FromArgb(30, 60, 90)
$BtnSearchOnline.ForeColor = $Blue
$BtnSearchOnline.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 9)
$BtnSearchOnline.TextAlign = "MiddleCenter"
$BtnSearchOnline.Cursor = "Hand"
$BtnSearchOnline.Visible = $false
$SidebarPanel.Controls.Add($BtnSearchOnline)
$Header.Controls.Add($BtnSearchOnline)

# --- ESTADO 3: Submenu Sistema (inicialmente ocultos) ---
$BtnWinUpdates = New-Object System.Windows.Forms.Button
$BtnWinUpdates.Text = "Win. Updates"
$BtnWinUpdates.Size = New-Object System.Drawing.Size(120, 32)
$BtnWinUpdates.Location = New-Object System.Drawing.Point(10, 55)
$BtnWinUpdates.FlatStyle = "Flat"
$BtnWinUpdates.FlatAppearance.BorderSize = 0
$BtnWinUpdates.BackColor = $SidebarBtn
$BtnWinUpdates.ForeColor = $Blue
$BtnWinUpdates.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 9)
$BtnWinUpdates.TextAlign = "MiddleCenter"
$BtnWinUpdates.Cursor = "Hand"
$BtnWinUpdates.Visible = $false
$SidebarPanel.Controls.Add($BtnWinUpdates)

$BtnDrivers = New-Object System.Windows.Forms.Button
$BtnDrivers.Text = "Drivers"
$BtnDrivers.Size = New-Object System.Drawing.Size(120, 32)
$BtnDrivers.Location = New-Object System.Drawing.Point(10, 95)
$BtnDrivers.FlatStyle = "Flat"
$BtnDrivers.FlatAppearance.BorderSize = 0
$BtnDrivers.BackColor = $SidebarBtn
$BtnDrivers.ForeColor = [System.Drawing.Color]::FromArgb(255, 180, 100)
$BtnDrivers.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 9)
$BtnDrivers.TextAlign = "MiddleCenter"
$BtnDrivers.Cursor = "Hand"
$BtnDrivers.Visible = $false
$SidebarPanel.Controls.Add($BtnDrivers)

$BtnActivator = New-Object System.Windows.Forms.Button
$BtnActivator.Text = "Ativador"
$BtnActivator.Size = New-Object System.Drawing.Size(120, 32)
$BtnActivator.Location = New-Object System.Drawing.Point(10, 135)
$BtnActivator.FlatStyle = "Flat"
$BtnActivator.FlatAppearance.BorderSize = 0
$BtnActivator.BackColor = $SidebarBtn
$BtnActivator.ForeColor = [System.Drawing.Color]::FromArgb(138, 43, 226)  # Roxo/Violeta
$BtnActivator.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 9)
$BtnActivator.TextAlign = "MiddleCenter"
$BtnActivator.Cursor = "Hand"
$BtnActivator.Visible = $false
$SidebarPanel.Controls.Add($BtnActivator)

$BtnLocalAccount = New-Object System.Windows.Forms.Button
$BtnLocalAccount.Text = "Conta Local"
$BtnLocalAccount.Size = New-Object System.Drawing.Size(120, 32)
$BtnLocalAccount.Location = New-Object System.Drawing.Point(10, 175)
$BtnLocalAccount.FlatStyle = "Flat"
$BtnLocalAccount.FlatAppearance.BorderSize = 0
$BtnLocalAccount.BackColor = $SidebarBtn
$BtnLocalAccount.ForeColor = [System.Drawing.Color]::FromArgb(236, 72, 153)  # Rosa/Magenta
$BtnLocalAccount.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 9)
$BtnLocalAccount.TextAlign = "MiddleCenter"
$BtnLocalAccount.Cursor = "Hand"
$BtnLocalAccount.Visible = $false
$SidebarPanel.Controls.Add($BtnLocalAccount)

$BtnScripts = New-Object System.Windows.Forms.Button
$BtnScripts.Text = "Scripts"
$BtnScripts.Size = New-Object System.Drawing.Size(120, 32)
$BtnScripts.Location = New-Object System.Drawing.Point(10, 215)
$BtnScripts.FlatStyle = "Flat"
$BtnScripts.FlatAppearance.BorderSize = 0
$BtnScripts.BackColor = $SidebarBtn
$BtnScripts.ForeColor = [System.Drawing.Color]::FromArgb(129, 199, 132)
$BtnScripts.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 9)
$BtnScripts.TextAlign = "MiddleCenter"
$BtnScripts.Cursor = "Hand"
$BtnScripts.Visible = $false
$SidebarPanel.Controls.Add($BtnScripts)

# --- ESTADO 4: Submenu Impressoras (inicialmente ocultos) ---
$BtnPrinterEpsonSC = New-Object System.Windows.Forms.Button
$BtnPrinterEpsonSC.Text = "Epson SC-T3170"
$BtnPrinterEpsonSC.Size = New-Object System.Drawing.Size(120, 32)
$BtnPrinterEpsonSC.Location = New-Object System.Drawing.Point(10, 55)
$BtnPrinterEpsonSC.FlatStyle = "Flat"
$BtnPrinterEpsonSC.FlatAppearance.BorderSize = 0
$BtnPrinterEpsonSC.BackColor = $SidebarBtn
$BtnPrinterEpsonSC.ForeColor = [System.Drawing.Color]::FromArgb(100, 181, 246)
$BtnPrinterEpsonSC.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 9)
$BtnPrinterEpsonSC.TextAlign = "MiddleCenter"
$BtnPrinterEpsonSC.Cursor = "Hand"
$BtnPrinterEpsonSC.Visible = $false
$SidebarPanel.Controls.Add($BtnPrinterEpsonSC)

$BtnPrinterCanonG3160 = New-Object System.Windows.Forms.Button
$BtnPrinterCanonG3160.Text = "Canon G3160"
$BtnPrinterCanonG3160.Size = New-Object System.Drawing.Size(120, 32)
$BtnPrinterCanonG3160.Location = New-Object System.Drawing.Point(10, 95)
$BtnPrinterCanonG3160.FlatStyle = "Flat"
$BtnPrinterCanonG3160.FlatAppearance.BorderSize = 0
$BtnPrinterCanonG3160.BackColor = $SidebarBtn
$BtnPrinterCanonG3160.ForeColor = [System.Drawing.Color]::FromArgb(100, 181, 246)
    $BtnPrinterCanonG3160.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 9)
    $BtnPrinterCanonG3160.TextAlign = "MiddleCenter"
$BtnPrinterCanonG3160.Cursor = "Hand"
$BtnPrinterCanonG3160.Visible = $false
$SidebarPanel.Controls.Add($BtnPrinterCanonG3160)

$BtnPrinterCanonG2060 = New-Object System.Windows.Forms.Button
$BtnPrinterCanonG2060.Text = "Canon G2060"
$BtnPrinterCanonG2060.Size = New-Object System.Drawing.Size(120, 32)
$BtnPrinterCanonG2060.Location = New-Object System.Drawing.Point(10, 135)
$BtnPrinterCanonG2060.FlatStyle = "Flat"
$BtnPrinterCanonG2060.FlatAppearance.BorderSize = 0
$BtnPrinterCanonG2060.BackColor = $SidebarBtn
$BtnPrinterCanonG2060.ForeColor = [System.Drawing.Color]::FromArgb(100, 181, 246)
    $BtnPrinterCanonG2060.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 9)
    $BtnPrinterCanonG2060.TextAlign = "MiddleCenter"
$BtnPrinterCanonG2060.Cursor = "Hand"
$BtnPrinterCanonG2060.Visible = $false
$SidebarPanel.Controls.Add($BtnPrinterCanonG2060)

$BtnPrinterElginL42 = New-Object System.Windows.Forms.Button
$BtnPrinterElginL42.Text = "Elgin L42Pro"
$BtnPrinterElginL42.Size = New-Object System.Drawing.Size(120, 32)
$BtnPrinterElginL42.Location = New-Object System.Drawing.Point(10, 175)
$BtnPrinterElginL42.FlatStyle = "Flat"
$BtnPrinterElginL42.FlatAppearance.BorderSize = 0
$BtnPrinterElginL42.BackColor = $SidebarBtn
$BtnPrinterElginL42.ForeColor = [System.Drawing.Color]::FromArgb(100, 181, 246)
    $BtnPrinterElginL42.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 9)
    $BtnPrinterElginL42.TextAlign = "MiddleCenter"
$BtnPrinterElginL42.Cursor = "Hand"
$BtnPrinterElginL42.Visible = $false
$SidebarPanel.Controls.Add($BtnPrinterElginL42)

$BtnPrinterArgoxOS = New-Object System.Windows.Forms.Button
$BtnPrinterArgoxOS.Text = "Argox OS-214Plus"
$BtnPrinterArgoxOS.Size = New-Object System.Drawing.Size(120, 32)
$BtnPrinterArgoxOS.Location = New-Object System.Drawing.Point(10, 215)
$BtnPrinterArgoxOS.FlatStyle = "Flat"
$BtnPrinterArgoxOS.FlatAppearance.BorderSize = 0
$BtnPrinterArgoxOS.BackColor = $SidebarBtn
$BtnPrinterArgoxOS.ForeColor = [System.Drawing.Color]::FromArgb(100, 181, 246)
    $BtnPrinterArgoxOS.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 9)
    $BtnPrinterArgoxOS.TextAlign = "MiddleCenter"
$BtnPrinterArgoxOS.Cursor = "Hand"
$BtnPrinterArgoxOS.Visible = $false
$SidebarPanel.Controls.Add($BtnPrinterArgoxOS)

# --- ESTADO 5: Submenu Cache (inicialmente ocultos) ---
$BtnStorageWinget = New-Object System.Windows.Forms.Button
$BtnStorageWinget.Text = "Motor WinGet"
$BtnStorageWinget.Size = New-Object System.Drawing.Size(120, 32)
$BtnStorageWinget.FlatStyle = "Flat"
$BtnStorageWinget.FlatAppearance.BorderColor = [System.Drawing.Color]::FromArgb(144, 202, 249)
$BtnStorageWinget.BackColor = $SidebarBtn
$BtnStorageWinget.ForeColor = [System.Drawing.Color]::FromArgb(144, 202, 249)
$BtnStorageWinget.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 9)
$BtnStorageWinget.TextAlign = "MiddleCenter"
$BtnStorageWinget.Cursor = "Hand"
$BtnStorageWinget.Visible = $false
$SidebarPanel.Controls.Add($BtnStorageWinget)

$BtnStorageApps = New-Object System.Windows.Forms.Button
$BtnStorageApps.Text = "Pacotes baixados"
$BtnStorageApps.Size = New-Object System.Drawing.Size(120, 32)
$BtnStorageApps.FlatStyle = "Flat"
$BtnStorageApps.FlatAppearance.BorderColor = $Green
$BtnStorageApps.BackColor = $SidebarBtn
$BtnStorageApps.ForeColor = $Green
$BtnStorageApps.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 9)
$BtnStorageApps.TextAlign = "MiddleCenter"
$BtnStorageApps.Cursor = "Hand"
$BtnStorageApps.Visible = $false
$SidebarPanel.Controls.Add($BtnStorageApps)

# --- RODAPE DA SIDEBAR (sempre visivel) ---
$BtnInstallWinget = New-Object System.Windows.Forms.Button
$BtnInstallWinget.Text = "Instalar Winget"
$BtnInstallWinget.Size = New-Object System.Drawing.Size(120, 28)
$BtnInstallWinget.FlatStyle = "Flat"
$BtnInstallWinget.FlatAppearance.BorderColor = $Blue
$BtnInstallWinget.BackColor = $Sidebar
$BtnInstallWinget.ForeColor = $Blue
$BtnInstallWinget.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 8)
$BtnInstallWinget.TextAlign = "MiddleCenter"
$BtnInstallWinget.Cursor = "Hand"
$SidebarPanel.Controls.Add($BtnInstallWinget)

$BtnRepairWinget = New-Object System.Windows.Forms.Button
$BtnRepairWinget.Text = "Reparar Winget"
$BtnRepairWinget.Size = New-Object System.Drawing.Size(120, 28)
$BtnRepairWinget.FlatStyle = "Flat"
$BtnRepairWinget.FlatAppearance.BorderColor = $Orange
$BtnRepairWinget.BackColor = $Sidebar
$BtnRepairWinget.ForeColor = $Orange
$BtnRepairWinget.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 8)
$BtnRepairWinget.TextAlign = "MiddleCenter"
$BtnRepairWinget.Cursor = "Hand"
$SidebarPanel.Controls.Add($BtnRepairWinget)

# ============================================
# CONTENT AREA - Posicionamento manual (85% da largura)
# ============================================
$ContentPanel = New-Object System.Windows.Forms.Panel
$ContentPanel.BackColor = $BG
$Form.Controls.Add($ContentPanel)

$ViewTitle = New-Object System.Windows.Forms.Label
$ViewTitle.Text = ""
$ViewTitle.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 14)
$ViewTitle.ForeColor = $Text
$ViewTitle.AutoSize = $true
$Header.Controls.Add($ViewTitle)

$ViewSubtitle = New-Object System.Windows.Forms.Label
$ViewSubtitle.Text = ""
$ViewSubtitle.Font = New-Object System.Drawing.Font("Segoe UI", 8.5)
$ViewSubtitle.ForeColor = $TextDim
$ViewSubtitle.AutoSize = $true
$Header.Controls.Add($ViewSubtitle)

# Mensagem inicial (area vazia)
$WelcomeLabel = New-Object System.Windows.Forms.Label
$WelcomeLabel.Text = "Selecione um modo no menu lateral"
$WelcomeLabel.Font = New-Object System.Drawing.Font("Segoe UI", 14)
$WelcomeLabel.ForeColor = $TextDim
$WelcomeLabel.AutoSize = $true
$ContentPanel.Controls.Add($WelcomeLabel)

# Lista de apps (inicialmente oculta)
$ListPanel = New-Object System.Windows.Forms.FlowLayoutPanel
$ListPanel.Dock = "Fill"
$ListPanel.BackColor = $BG
$ListPanel.AutoScroll = $true
$ListPanel.FlowDirection = "TopDown"
$ListPanel.WrapContents = $false
$ListPanel.Padding = New-Object System.Windows.Forms.Padding(10, 10, 25, 10)
$ListPanel.Visible = $false
$ContentPanel.Controls.Add($ListPanel)

function Set-ViewContext {
    param(
        [string]$TitleText,
        [string]$SubtitleText = "",
        [bool]$ShowHeader = $true
    )

    $ViewTitle.Text = $TitleText
    $ViewSubtitle.Text = $SubtitleText
    $ViewTitle.Visible = $ShowHeader
    $ViewSubtitle.Visible = $ShowHeader
}

#endregion
