#region [04-UI-THEME] Paleta visual, estilo e helpers visuais

$BG = [System.Drawing.Color]::FromArgb(30, 30, 35)
$Panel = [System.Drawing.Color]::FromArgb(40, 40, 48)
$Sidebar = [System.Drawing.Color]::FromArgb(35, 35, 42)
$Accent = [System.Drawing.Color]::FromArgb(99, 102, 241)
$Green = [System.Drawing.Color]::FromArgb(34, 197, 94)
$Yellow = [System.Drawing.Color]::FromArgb(234, 179, 8)
$Red = [System.Drawing.Color]::FromArgb(239, 68, 68)
$Blue = [System.Drawing.Color]::FromArgb(59, 130, 246)
$Orange = [System.Drawing.Color]::FromArgb(249, 115, 22)
$Text = [System.Drawing.Color]::White
$TextDim = [System.Drawing.Color]::FromArgb(160, 160, 175)
$ItemBG = [System.Drawing.Color]::FromArgb(50, 50, 60)
$ItemEss = [System.Drawing.Color]::FromArgb(45, 55, 50)
$LogBG = [System.Drawing.Color]::FromArgb(25, 25, 30)
$SidebarBtn = [System.Drawing.Color]::FromArgb(45, 45, 55)
$SidebarBtnActive = [System.Drawing.Color]::FromArgb(60, 60, 75)

# ============================================
# LAYOUT - Detectar resolucao
# ============================================
$screen = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
$formWidth = [Math]::Min([Math]::Max(900, [int]($screen.Width * 0.65)), 1400)
$formHeight = [Math]::Min([Math]::Max(600, [int]($screen.Height * 0.85)), 950)

# ============================================
# FORMULARIO PRINCIPAL
# ============================================
Write-FileLog "Criando formulario principal..." "INFO"
try {
    $Form = New-Object System.Windows.Forms.Form
    $Form.Text = "BananaSuisa v$($script:BananaSuisaVersao) [Administrador]"
    $Form.ClientSize = New-Object System.Drawing.Size($formWidth, $formHeight)
    $Form.StartPosition = "CenterScreen"
    $Form.FormBorderStyle = "Sizable"
    $Form.MinimumSize = New-Object System.Drawing.Size(800, 550)
    $Form.BackColor = $BG
    $Form.Font = New-Object System.Drawing.Font("Segoe UI", 9)
    Write-FileLog "Formulario criado com sucesso" "INFO"
    Save-LogToFile | Out-Null
} catch {
    Write-FileLog "ERRO ao criar formulario: $_" "ERROR"
    throw
}

# Aplicar tema do sistema na barra de titulo
Write-FileLog "Aplicando tema do sistema..." "INFO"
Set-WindowTheme -Form $Form

# Evento de fechamento - salvar logs e encerrar processos pendentes
$Form.Add_FormClosing({
    param($sender, $e)

    # Se houver operacao em andamento, confirmar e forcar encerramento
    if ($script:Installing) {
        $script:CancelRequested = $true
        if ($script:CurrentProcess -and -not $script:CurrentProcess.HasExited) {
            try { $script:CurrentProcess.Kill() } catch {}
        }
        $script:Installing = $false
    }

    if ($script:SearchDebounceTimer) {
        try {
            $script:SearchDebounceTimer.Stop()
            $script:SearchDebounceTimer.Dispose()
        } catch {}
    }
    
    # Salvar logs silenciosamente ao fechar
    if ($script:LogEntries.Count -gt 0) {
        Save-LogToFile | Out-Null
    }
})

#endregion
