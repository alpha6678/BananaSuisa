#region [11-EVENTS] Eventos e inicializacao principal

# ============================================
# EVENTOS
# ============================================
$Form.Add_Load({
    try {
        Write-FileLog "Form_Load: Iniciando configuracao inicial..." "INFO"
        Update-Layout
        Write-FileLog "Form_Load: Layout atualizado." "INFO"
    } catch {
        Write-FileLog "ERRO CRITICO no Form_Load: $_" "ERROR"
        Save-LogToFile | Out-Null
    }
})

$Form.Add_Shown({
    try {
        Write-FileLog "Form_Shown: Iniciando bootstrap de dados..." "INFO"
        Write-Log "BananaSuisa v$($script:BananaSuisaVersao) iniciado" -Type "Success"
        
        # 1. Inicializar Workspace (Filesystem e caminhos finais)
        Write-FileLog "Bootstrap: Inicializando Workspace..." "INFO"
        Initialize-Workspace
        Write-FileLog "Bootstrap: Workspace OK." "INFO"
        Save-LogToFile | Out-Null

        # 2. Verificação de ambiente WinGet
        Write-FileLog "Bootstrap: Verificando instalacao do WinGet..." "INFO"
        if (-not (Test-WingetInstalled)) {
            Write-Log "Winget nao encontrado!" -Type "Warning"
            Write-FileLog "Bootstrap: WinGet nao detectado." "WARNING"
        } else {
            $version = & (Get-BananaSuisaWingetExe) --version 2>&1
            Write-Log "Winget: $version" -Type "Info"
            Write-FileLog "Bootstrap: WinGet detectado (v$version)." "INFO"
        }
        
        Write-FileLog "Bootstrap: Concluido com sucesso." "INFO"
        Save-LogToFile | Out-Null
    } catch {
        Write-FileLog "ERRO CRITICO no Form_Shown: $_" "ERROR"
        Write-Log "Erro na inicializacao: $_" -Type "Error"
        Save-LogToFile | Out-Null
    }
})

$Form.Add_Resize({ Update-Layout })

$BtnModeInstall.Add_Click({ Show-InstallMode })
$BtnModeUpdate.Add_Click({ Show-UpdateMode })
$BtnModeRemove.Add_Click({ Show-RemoveMode })
$BtnModeSystem.Add_Click({ Show-SystemMode })
$BtnModePrinters.Add_Click({ Show-PrintersMode })
$BtnModeStorage.Add_Click({ Show-StorageMode })

# Eventos do submenu Sistema
$BtnWinUpdates.Add_Click({ Show-WindowsUpdatesMode })
$BtnDrivers.Add_Click({ Show-MissingDriversMode })
$BtnActivator.Add_Click({ Show-ActivatorMode })
$BtnLocalAccount.Add_Click({ Show-LocalAccountMode })
$BtnScripts.Add_Click({ Show-ScriptsMode })

# Eventos do submenu Impressoras
$BtnPrinterEpsonSC.Add_Click({
# ... (existing Epson code)
})

# Eventos do submenu Cache
$BtnStorageWinget.Add_Click({
    if (-not $script:Installing) {
        $script:Installing = $true
        Update-WinGetCache
        $script:Installing = $false
        [System.Windows.Forms.MessageBox]::Show("Cache de sistema WinGet atualizado com sucesso!", "Sucesso", "OK", "Information")
    }
})

$BtnStorageApps.Add_Click({ Show-ManageInstallersMode })

$BtnPrinterEpsonSC.Add_Click({
    if (-not $script:Installing) {
        $result = Install-PrinterDriver -PrinterName "Epson SC-T3170" -DownloadUrl "https://ftp.epson.com/drivers/SCT3170M_Combo_AM.exe" -FileName "SCT3170M_Combo_AM.exe"
        if ($result.Success) {
            [System.Windows.Forms.MessageBox]::Show("Driver Epson SC-T3170 instalado com sucesso!", "Sucesso", "OK", "Information")
        } else {
            [System.Windows.Forms.MessageBox]::Show("Erro ao instalar driver: $($result.Message)", "Erro", "OK", "Error")
        }
    }
})

$BtnPrinterCanonG3160.Add_Click({
    if (-not $script:Installing) {
        # URL compatível com a família de drivers Canon G-series; validação de modelo mantida no botão do botão correto.
        $result = Install-PrinterDriver -PrinterName "Canon G3160" -DownloadUrl "https://gdlp01.c-wss.com/gds/4/0100010914/03/md__-win-g3020_g3060-1_03-ea34_2.exe" -FileName "canon_g3160_driver.exe"
        if ($result.Success) {
            [System.Windows.Forms.MessageBox]::Show("Driver Canon G3160 instalado com sucesso!", "Sucesso", "OK", "Information")
        } else {
            [System.Windows.Forms.MessageBox]::Show("Erro ao instalar driver: $($result.Message)", "Erro", "OK", "Error")
        }
    }
})

$BtnPrinterCanonG2060.Add_Click({
    if (-not $script:Installing) {
        $result = Install-PrinterDriver -PrinterName "Canon G2060" -DownloadUrl "https://gdlp01.c-wss.com/gds/5/0100010915/03/md__-win-g2020_g2060-1_03-ea34_2.exe" -FileName "canon_g2060_driver.exe"
        if ($result.Success) {
            [System.Windows.Forms.MessageBox]::Show("Driver Canon G2060 instalado com sucesso!", "Sucesso", "OK", "Information")
        } else {
            [System.Windows.Forms.MessageBox]::Show("Erro ao instalar driver: $($result.Message)", "Erro", "OK", "Error")
        }
    }
})

$BtnPrinterElginL42.Add_Click({
    if (-not $script:Installing) {
        $result = Install-PrinterDriver -PrinterName "Elgin L42Pro" -DownloadUrl "https://natsys.com.br/downloads/drivers/impressoras/Elgin%20L42%20Pro/driver-elgin-l42-pro.zip" -FileName "driver-elgin-l42-pro.zip"
        if ($result.Success) {
            [System.Windows.Forms.MessageBox]::Show("Driver Elgin L42Pro extraido com sucesso!`n`nA pasta com o instalador foi aberta.`nExecute o setup para instalar o driver.", "Sucesso", "OK", "Information")
        } else {
            [System.Windows.Forms.MessageBox]::Show("Erro ao baixar driver: $($result.Message)", "Erro", "OK", "Error")
        }
    }
})

$BtnPrinterArgoxOS.Add_Click({
    if (-not $script:Installing) {
        $result = Install-PrinterDriver -PrinterName "Argox OS-214Plus" -DownloadUrl "https://www.argox.com/docfile/drivers/Argox_11.10.0.exe" -FileName "Argox_11.10.0.exe"
        if ($result.Success) {
            [System.Windows.Forms.MessageBox]::Show("Driver Argox OS-214Plus instalado com sucesso!", "Sucesso", "OK", "Information")
        } else {
            [System.Windows.Forms.MessageBox]::Show("Erro ao instalar driver: $($result.Message)", "Erro", "OK", "Error")
        }
    }
})

$BtnBack.Add_Click({ 
    if (-not $script:Installing) {
        # Verificar se estamos no submenu Sistema
        if ($script:CurrentMode -eq "System" -or $script:CurrentMode -eq "Cache") {
            Reset-ToMainMenu
            Write-Log "Retornando ao menu principal" -Type "Info"
        } elseif ($script:CurrentMode -eq "Printers") {
            Reset-ToMainMenu
            Write-Log "Retornando ao menu principal" -Type "Info"
        } elseif ($script:CurrentMode -eq "WindowsUpdates" -or $script:CurrentMode -eq "Drivers" -or $script:CurrentMode -eq "Scripts") {
            Show-SystemMode
            Write-Log "Retornando ao menu Sistema" -Type "Info"
        } elseif ($script:CurrentMode -eq "ManageInstallers") {
            Show-CacheMode
            Write-Log "Retornando ao menu Cache" -Type "Info"
        } elseif ($script:CurrentMode -eq "InstallOffline") {
            Show-InstallMode
            Write-Log "Retornando ao menu Instalar" -Type "Info"
        } else {
            Reset-ToMainMenu
            Write-Log "Retornando ao menu principal" -Type "Info"
        }
    }
})

$BtnAll.Add_Click({ 
    foreach ($cb in $script:Checkboxes) { $cb.Checked = $true }
    & $script:UpdateCount
})

$BtnNone.Add_Click({ 
    foreach ($cb in $script:Checkboxes) { $cb.Checked = $false }
    & $script:UpdateCount
})

# Evento do botao Buscar Online
$BtnSearchOnline.Add_Click({
    $searchText = $SearchBox.Text
    
    # Verificar se o texto e valido (nao e placeholder)
    if ($searchText -eq $SearchPlaceholder -or [string]::IsNullOrWhiteSpace($searchText)) {
        [System.Windows.Forms.MessageBox]::Show(
            "Digite um termo de busca no campo acima e clique em 'Buscar Online'.",
            "Buscar no Winget",
            "OK",
            "Information"
        )
        $SearchBox.Focus()
        return
    }
    
    if ($searchText.Length -lt 2) {
        [System.Windows.Forms.MessageBox]::Show(
            "Digite pelo menos 2 caracteres para buscar.",
            "Buscar no Winget",
            "OK",
            "Warning"
        )
        return
    }
    
    # Limpar lista atual
    $ListPanel.Controls.Clear()
    $script:Checkboxes = @()
    $script:AppItems = @()
    
    # Mostrar que esta buscando
    $ListPanel.SuspendLayout()
    
    # Buscar online
    $results = Search-WingetOnline -SearchTerm $searchText
    
    if ($results.Count -eq 0) {
        $noResults = New-Object System.Windows.Forms.Label
        $noResults.Text = "Nenhum pacote encontrado para '$searchText'"
        $noResults.Font = New-Object System.Drawing.Font("Segoe UI", 11)
        $noResults.ForeColor = $TextDim
        $noResults.AutoSize = $true
        $noResults.Margin = New-Object System.Windows.Forms.Padding(20)
        $ListPanel.Controls.Add($noResults)
    } else {
        # Verificar quais ja estao instalados
        $installedApps = Get-InstalledApps
        
        foreach ($pkg in $results) {
            # Verificar se ja esta instalado
            $isInstalled = $false
            if ($installedApps) {
                $pkgIdLower = $pkg.I.ToLower()
                $pkgNameLower = $pkg.N.ToLower()
                foreach ($inst in $installedApps) {
                    $instIdLower = if ($inst.I) { $inst.I.ToLower() } else { "" }
                    $instNameLower = if ($inst.N) { $inst.N.ToLower() } else { "" }
                    if ($instIdLower -eq $pkgIdLower -or $instNameLower -eq $pkgNameLower) {
                        $isInstalled = $true
                        break
                    }
                }
            }
            
            $extraInfo = if ($pkg.Version) { "v$($pkg.Version)" } else { "" }
            $item = New-AppItem -App $pkg -ExtraInfo $extraInfo -IsEssential $false -Source "Online" -IsInstalled $isInstalled
            $ListPanel.Controls.Add($item)
        }
    }
    
    $ListPanel.ResumeLayout($true)
    [System.Windows.Forms.Application]::DoEvents()
    Update-Layout
    & $script:UpdateCount
})

$BtnInstallWinget.Add_Click({
    $r = [System.Windows.Forms.MessageBox]::Show(
        "Instalar/reinstalar o Winget com todas as dependencias?`n`n(VCLibs, UI.Xaml e WinGet)",
        "Instalar Winget",
        "YesNo",
        "Question"
    )
    if ($r -eq "Yes") {
        Write-Log "Instalando Winget e dependencias..." -Type "Progress"
        try {
            Install-WingetComplete
            Write-Log "Dependencias instaladas com sucesso!" -Type "Success"
            [System.Windows.Forms.MessageBox]::Show(
                "WinGet e dependencias instalados com sucesso!",
                "Sucesso",
                "OK",
                "Information"
            )
        } catch {
            Write-Log "Erro no instalador: $_" -Type "Error"
            [System.Windows.Forms.MessageBox]::Show(
                "Falha ao instalar WinGet. Tente novamente.",
                "Falha",
                "OK",
                "Error"
            )
        }
        
        Write-Log "Processo de instalacao concluido" -Type "Info"
    }
})

$BtnRepairWinget.Add_Click({
    $r = [System.Windows.Forms.MessageBox]::Show(
        "Reparar o Winget (limpar cache, reinstalar)?",
        "Reparar Winget",
        "YesNo",
        "Warning"
    )
    if ($r -eq "Yes") {
        try {
            Repair-WingetComplete
            Write-Log "Reparo do Winget concluido" -Type "Info"
            [System.Windows.Forms.MessageBox]::Show(
                "Reparo concluido com sucesso.",
                "Reparo",
                [System.Windows.Forms.MessageBoxButtons]::OK,
                [System.Windows.Forms.MessageBoxIcon]::Information
            )
        } catch {
            Write-Log "Erro ao reparar Winget: $_" -Type "Error"
            [System.Windows.Forms.MessageBox]::Show(
                "Falha ao reparar o Winget. Verifique os logs.",
                "Falha no reparo",
                [System.Windows.Forms.MessageBoxButtons]::OK,
                [System.Windows.Forms.MessageBoxIcon]::Error
            )
        }
    }
})

# ============================================
# EVENTO PRINCIPAL - EXECUTAR OU CANCELAR ACAO
# ============================================
$BtnAction.Add_Click({
    # Se estiver instalando, o botao funciona como CANCELAR
    if ($script:Installing) {
        $r = [System.Windows.Forms.MessageBox]::Show(
            "Deseja realmente cancelar?",
            "Cancelar",
            "YesNo",
            "Warning"
        )
        if ($r -eq "Yes") {
            $script:CancelRequested = $true
            Write-Log "Cancelamento solicitado..." -Type "Warning"
            if ($script:CurrentProcess -and -not $script:CurrentProcess.HasExited) {
                try { $script:CurrentProcess.Kill() } catch {}
            }
        }
        return
    }
    
    # Se nao estiver instalando, funciona como EXECUTAR
    if (-not $script:CurrentMode) { return }
    
    # Verificar winget apenas para modos que precisam dele
    if ($script:CurrentMode -in @("Install", "Update", "Remove", "ManageInstallers")) {
        if (-not (Test-WingetInstalled)) {
            $r = [System.Windows.Forms.MessageBox]::Show(
                "Winget nao esta instalado!`n`nDeseja instalar agora?",
                "Winget Necessario",
                "YesNo",
                "Warning"
            )
            if ($r -eq "Yes") {
                try {
                    Install-WingetComplete
                    [System.Windows.Forms.MessageBox]::Show(
                        "Winget instalado com sucesso!",
                        "Instalacao concluida",
                        "OK",
                        "Information"
                    )
                } catch {
                    [System.Windows.Forms.MessageBox]::Show(
                        "Falha ao instalar o Winget. Use o botao 'Instalar Winget' para nova tentativa.",
                        "Falha",
                        "OK",
                        "Error"
                    )
                }
            }
            return
        }
    }
    
    $selected = @($script:Checkboxes | Where-Object { $_.Checked } | ForEach-Object { $_.Tag })
    $totalApps = $selected.Length
    
    if ($totalApps -eq 0) {
        [System.Windows.Forms.MessageBox]::Show("Selecione pelo menos um item!", "Aviso", "OK", "Warning")
        return
    }
    
    $actionText = switch ($script:CurrentMode) {
        "Install" { "instalar" }
        "InstallOffline" { "instalar offline" }
        "Update" { "atualizar" }
        "Remove" { "remover" }
        "ManageInstallers" { "baixar para pasta" }
        "WindowsUpdates" { "instalar atualizacao(oes)" }
        "Drivers" { "atualizar driver(s)" }
        "Scripts" { "executar script(s)" }
    }
    
    $r = [System.Windows.Forms.MessageBox]::Show(
        "Deseja $actionText $totalApps item(s)?",
        "Confirmar",
        "YesNo",
        "Question"
    )
    
    if ($r -ne "Yes") { return }
    
    $script:Installing = $true
    $script:CancelRequested = $false
    $script:InstallResults = @{ Success = @(); Failed = @(); Skipped = @(); RebootRequired = @() }
    
    # Mudar botao para modo CANCELAR
    $BtnAction.Text = "CANCELAR"
    $BtnAction.BackColor = $Red
    $BtnAction.ForeColor = $Text
    
    $BtnBack.Enabled = $false
    $BtnAll.Enabled = $false
    $BtnNone.Enabled = $false
    foreach ($cb in $script:Checkboxes) { $cb.Enabled = $false }
    
    Write-Log "Iniciando $actionText de $totalApps item(s)..." -Type "Info"
    
    $current = 0
    foreach ($app in $selected) {
        if ($script:CancelRequested) {
            Write-Log "Operacao cancelada." -Type "Warning"
            break
        }
        
        $current++
        $StageLabel.Text = "[$current/$totalApps] $($app.N)"
        $StageLabel.Visible = $true
        [System.Windows.Forms.Application]::DoEvents()
        
        $result = switch ($script:CurrentMode) {
            "Install" { Install-AppWithWinget -AppId $app.I -AppName $app.N }
            "InstallOffline" { Install-AppWithWinget -AppId $app.I -AppName $app.N -OfflineOnly $true }
            "Update" { Update-AppWithWinget -AppId $app.I -AppName $app.N }
            "Remove" { Remove-AppWithWinget -AppId $app.I -AppName $app.N }
            "ManageInstallers" { 
                Write-FileLog "ManageInstallers: Processando $($app.N) ($($app.I))" "INFO"
                Update-LogProgress "$($app.N)" "[Consultando versao...]"
                [System.Windows.Forms.Application]::DoEvents()
                $latest = Get-WingetAppLatestVersion -AppId $app.I
                if ($latest) {
                    $cached = Get-LocalInstaller -AppId $app.I -Version $latest
                    if ($cached) {
                        Write-FileLog "ManageInstallers: $($app.I) v$latest ja disponivel localmente" "INFO"
                        @{ Success = $true; Message = "Ja disponivel (v$latest)" }
                    } else {
                        Write-FileLog "ManageInstallers: Iniciando download de $($app.I) v$latest" "INFO"
                        $path = Download-ToInstallers -AppId $app.I -Version $latest -AppName $app.N
                        if ($path) { 
                            Write-FileLog "ManageInstallers: $($app.I) salvo com sucesso" "INFO"
                            @{ Success = $true; Message = "Salvo localmente (v$latest)" } 
                        } else { 
                            Write-FileLog "ManageInstallers: Falha no download de $($app.I)" "ERROR"
                            @{ Success = $false; Message = "Falha no download" } 
                        }
                    }
                } else {
                    Write-FileLog "ManageInstallers: Versao nao encontrada para $($app.I)" "WARN"
                    @{ Success = $false; Message = "Versao nao encontrada" }
                }
            }
            "WindowsUpdates" { Install-WindowsUpdateItem -UpdateItem $app }
            "Drivers" { Update-DriverItem -DriverItem $app }
            "Scripts" { Invoke-SystemScript -ScriptItem $app }
        }
        
        if ($result.Success) {
            Write-Log "$($app.N): $($result.Message)" -Type "Success"
            Write-FileLog "$($script:CurrentMode): SUCESSO em '$($app.N)' - $($result.Message)" "INFO"
            $script:InstallResults.Success += $app.N
            if ($result.RebootRequired) {
                $script:InstallResults.RebootRequired += $app.N
            }
            
            # Salvar app customizado se foi instalado ou baixado via busca online
            if (($script:CurrentMode -eq "Install" -or $script:CurrentMode -eq "ManageInstallers") -and $app.Source -eq "Online") {
                $saved = Add-CustomApp -AppName $app.N -AppId $app.I -Category "Online"
                if ($saved) {
                    # Adicionar imediatamente ao array em memoria
                    $exists = $script:Apps | Where-Object { $_.I -eq $app.I }
                    if (-not $exists) {
                        $script:Apps += @{
                            N = $app.N
                            I = $app.I
                            C = "Online"
                            E = $false
                        }
                    }
                    Write-Log "$($app.N) adicionado a lista de favoritos" -Type "Info"
                }
            }
        } else {
            Write-Log "$($app.N): $($result.Message)" -Type "Error"
            Write-FileLog "$($script:CurrentMode): FALHA em '$($app.N)' - $($result.Message)" "ERROR"
            $script:InstallResults.Failed += $app.N
        }
    }
    
    Write-Log "Operacao concluida!" -Type "Success"
    Write-FileLog "Operacao $($script:CurrentMode) concluida. Sucesso: $($script:InstallResults.Success.Count) / Falha: $($script:InstallResults.Failed.Count)" "INFO"
    Save-LogToFile | Out-Null
    Update-Stage "Waiting"
    $StageLabel.Text = ""
    $StageLabel.Visible = $false
    
    $script:Installing = $false
    
    # Restaurar botao para modo EXECUTAR
    $actionBtnText = switch ($script:CurrentMode) {
        "Install" { "INSTALAR" }
        "InstallOffline" { "INSTALAR OFFLINE" }
        "Update" { "ATUALIZAR" }
        "Remove" { "REMOVER" }
        "ManageInstallers" { "BAIXAR PARA PASTA" }
        "WindowsUpdates" { "INSTALAR" }
        "Drivers" { "ATUALIZAR" }
        "Scripts" { "EXECUTAR" }
    }
    $actionBtnColor = switch ($script:CurrentMode) {
        "Install" { $Green }
        "InstallOffline" { $Green }
        "Update" { $Blue }
        "Remove" { $Red }
        "ManageInstallers" { $Green }
        "WindowsUpdates" { $Blue }
        "Drivers" { [System.Drawing.Color]::FromArgb(255, 180, 100) }
        "Scripts" { [System.Drawing.Color]::FromArgb(129, 199, 132) }
    }
    $BtnAction.Text = $actionBtnText
    $BtnAction.BackColor = $actionBtnColor
    $BtnAction.ForeColor = $Text
    
    $BtnBack.Enabled = $true
    $BtnAll.Enabled = $true
    $BtnNone.Enabled = $true
    foreach ($cb in $script:Checkboxes) { $cb.Enabled = $true }
    
    Show-Report
    
    # Atualizar lista apos operacao para refletir mudancas
    if ($script:CurrentMode -eq "Remove") {
        Write-Log "Atualizando lista de apps instalados..." -Type "Info"
        Show-RemoveMode
    } elseif ($script:CurrentMode -eq "Update") {
        Write-Log "Atualizando lista de atualizacoes..." -Type "Info"
        Show-UpdateMode
    } elseif ($script:CurrentMode -eq "Install") {
        # Atualizar lista de instalacao para mostrar novos apps instalados
        Show-InstallMode
    } elseif ($script:CurrentMode -eq "InstallOffline") {
        Show-InstallOfflineMode
    } elseif ($script:CurrentMode -eq "ManageInstallers") {
        # Manter no modo cache
        Show-ManageInstallersMode
    } elseif ($script:CurrentMode -eq "WindowsUpdates") {
        Write-Log "Atualizando lista de Windows Updates..." -Type "Info"
        Show-WindowsUpdatesMode
    } elseif ($script:CurrentMode -eq "Drivers") {
        Write-Log "Atualizando lista de drivers..." -Type "Info"
        Show-MissingDriversMode
    } elseif ($script:CurrentMode -eq "Scripts") {
        Write-Log "Atualizando lista de scripts..." -Type "Info"
        Show-ScriptsMode
    }
})

# ============================================
# EXIBIR FORMULARIO
# ============================================
Write-FileLog "Exibindo formulario..." "INFO"
Write-FileLog "========================================" "INFO"
Save-LogToFile | Out-Null

try {
    [void]$Form.ShowDialog()
    Write-FileLog "Formulario fechado normalmente" "INFO"
} catch {
    Write-FileLog "ERRO durante execucao do formulario: $_" "ERROR"
    Write-FileLog "Stack: $($_.ScriptStackTrace)" "ERROR"
    Save-LogToFile | Out-Null
} finally {
    Write-FileLog "Encerrando BananaSuisa" "INFO"
    Write-FileLog "========================================" "INFO"
    Save-LogToFile | Out-Null
}
