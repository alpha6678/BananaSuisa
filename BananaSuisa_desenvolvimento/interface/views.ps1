#region [08-UI-VIEWS] Modos de tela, listas e formularios

# ============================================
# FUNCAO SHOW-INSTALL-MODE
# ============================================
function Show-InstallMode {
    $script:CurrentMode = "Install"
    Set-ViewContext -TitleText "Instalar aplicativos" -SubtitleText "Monte a selecao por categoria, perfil ou busca no catalogo e no Winget online." -ShowHeader $true
    $WelcomeLabel.Visible = $false
    $SearchBox.Visible = $true
    $ListPanel.Visible = $true
    $ListPanel.Controls.Clear()
    $script:Checkboxes = @()
    $script:AppItems = @()
    
    # Limpar campo de busca
    Set-SearchBoxText -Text $SearchPlaceholder -ForeColor $TextDim
    
    # Mudar sidebar para estado ativo (com Buscar Online no header)
    Show-SidebarActive -ShowEssentials $true
    $BtnAction.Text = "INSTALAR"
    $BtnAction.BackColor = $Green
    
    Write-Log "Modo INSTALAR selecionado" -Type "Info"
    
    # Forcar atualizacao do layout antes de criar itens
    [System.Windows.Forms.Application]::DoEvents()
    
    # Verificar quais apps ja estao instalados
    Update-LogProgress "Verificando apps instalados..."
    $installedApps = Get-InstalledApps
    Complete-LogProgress
    
    $installedCount = 0
    $ListPanel.SuspendLayout()
    foreach ($app in $script:Apps) {
        $isInstalled = Test-AppAlreadyInstalled -AppToCheck $app -InstalledList $installedApps
        if ($isInstalled) { $installedCount++ }
        $item = New-AppItem -App $app -IsEssential $app.E -Source "list" -IsInstalled $isInstalled
        $ListPanel.Controls.Add($item)
    }
    $ListPanel.ResumeLayout($true)
    
    if ($installedCount -gt 0) {
        Write-Log "$installedCount app(s) ja instalado(s)" -Type "Info"
    }
    
    # Forcar atualizacao e ajustar larguras
    [System.Windows.Forms.Application]::DoEvents()
    Update-Layout
    & $script:UpdateCount
}

function Show-InstallOfflineMode {
    $script:CurrentMode = "InstallOffline"
    Set-ViewContext -TitleText "Instalar aplicativos offline" -SubtitleText "Selecione os programas ja disponiveis na pasta local para instalar sem novo download." -ShowHeader $true
    $WelcomeLabel.Visible = $false
    $SearchBox.Visible = $true
    $ListPanel.Visible = $true
    $ListPanel.Controls.Clear()
    $script:Checkboxes = @()
    $script:AppItems = @()
    
    Set-SearchBoxText -Text $SearchPlaceholder -ForeColor $TextDim
    
    Show-SidebarActive -ShowEssentials $true
    $BtnAction.Text = "INSTALAR OFFLINE"
    $BtnAction.BackColor = $Green
    
    Write-Log "Modo INSTALAR OFFLINE selecionado" -Type "Info"
    [System.Windows.Forms.Application]::DoEvents()
    
    Update-LogProgress "Verificando apps instalados..."
    $installedApps = Get-InstalledApps
    Complete-LogProgress
    
    $offlineApps = @()
    $knownIds = $script:Apps | ForEach-Object { $_.I.ToLower() }
    
    # 1. Apps conhecidos da lista
    foreach ($app in ($script:Apps | Sort-Object C, N)) {
        $cached = Get-LatestLocalInstallerRecord -AppId $app.I
        if ($cached) {
            $offlineApps += @{
                N = $app.N
                I = $app.I
                C = $app.C
                E = $app.E
                CachedVersion = $cached.Version
                CachedPath = $cached.Path
            }
        }
    }

    # 2. Scannear cache por arquivos nao listados (IDs desconhecidos)
    if ($script:UseWorkspace -and (Test-Path $script:AppPaths.Installers)) {
        $cacheFiles = Get-ChildItem -Path $script:AppPaths.Installers -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match "^(.+)_(.+)\.(exe|msi|msix|msixbundle|appx|appxbundle|zip)$" }
            
        foreach ($file in $cacheFiles) {
            if ($file.Name -match "^(.+)_(.+)\.") {
                $foundId = $Matches[1]
                $foundVer = $Matches[2]
                
                if ($knownIds -notcontains $foundId.ToLower()) {
                    # Encontrou um instalador localmente que nao esta na lista principal
                    $offlineApps += @{
                        N = $foundId # Usamos o ID como nome ja que nao sabemos o original
                        I = $foundId
                        C = "Extraido do Cache"
                        E = $false
                        CachedVersion = $foundVer
                        CachedPath = $file.FullName
                    }
                    $knownIds += $foundId.ToLower() # Evitar duplicados se houver varias versoes
                }
            }
        }
    }
    
    $ListPanel.SuspendLayout()
    if ($offlineApps.Count -eq 0) {
        $empty = New-Object System.Windows.Forms.Label
        $empty.Text = "Nenhum instalador offline encontrado localmente local."
        $empty.Font = New-Object System.Drawing.Font("Segoe UI", 11)
        $empty.ForeColor = $TextDim
        $empty.AutoSize = $true
        $empty.Margin = New-Object System.Windows.Forms.Padding(20)
        $ListPanel.Controls.Add($empty)
        Write-Log "Nenhum instalador offline disponivel localmente." -Type "Warning"
    } else {
        foreach ($app in $offlineApps) {
            $isInstalled = Test-AppAlreadyInstalled -AppToCheck $app -InstalledList $installedApps
            $extraInfo = if ($app.CachedVersion) { "cache v$($app.CachedVersion)" } else { "cache local" }
            $item = New-AppItem -App $app -ExtraInfo $extraInfo -IsEssential $app.E -Source "Offline" -IsInstalled $isInstalled
            $ListPanel.Controls.Add($item)
        }
        Write-Log "$($offlineApps.Count) instalador(es) offline disponivel(is) localmente." -Type "Success"
    }
    $ListPanel.ResumeLayout($true)
    
    [System.Windows.Forms.Application]::DoEvents()
    Update-Layout
    & $script:UpdateCount
}

# ============================================
# FUNCAO SHOW-UPDATE-MODE
# ============================================
function Show-UpdateMode {
    $script:CurrentMode = "Update"
    Set-ViewContext -TitleText "Atualizar aplicativos" -SubtitleText "Revise atualizacoes do catalogo e do sistema com melhor separacao visual." -ShowHeader $true
    $WelcomeLabel.Visible = $false
    $SearchBox.Visible = $true
    $ListPanel.Visible = $true
    $ListPanel.Controls.Clear()
    $script:Checkboxes = @()
    $script:AppItems = @()
    
    # Limpar campo de busca
    Set-SearchBoxText -Text $SearchPlaceholder -ForeColor $TextDim
    
    # Mudar sidebar para estado ativo (sem Buscar Online)
    Show-SidebarActive -ShowEssentials $false
    $BtnAction.Text = "ATUALIZAR"
    $BtnAction.BackColor = $Blue
    
    Write-Log "Modo ATUALIZAR selecionado" -Type "Info"
    [System.Windows.Forms.Application]::DoEvents()
    
    $script:AvailableUpdates = Get-WingetUpdates
    
    $ListPanel.SuspendLayout()
    
    if ($script:AvailableUpdates.Count -eq 0) {
        $noUpdates = New-Object System.Windows.Forms.Label
        $noUpdates.Text = "Nenhuma atualizacao disponivel!"
        $noUpdates.Font = New-Object System.Drawing.Font("Segoe UI", 12)
        $noUpdates.ForeColor = $Green
        $noUpdates.AutoSize = $true
        $noUpdates.Margin = New-Object System.Windows.Forms.Padding(20)
        $ListPanel.Controls.Add($noUpdates)
    } else {
        $ourListIds = $script:Apps | ForEach-Object { $_.I }
        $ourUpdates = @()
        $systemUpdates = @()
        
        foreach ($upd in $script:AvailableUpdates) {
            if ($ourListIds -contains $upd.I) {
                $ourUpdates += $upd
            } else {
                $systemUpdates += $upd
            }
        }
        
        if ($ourUpdates.Count -gt 0) {
            $divider1 = New-Divider -Text "ATUALIZACOES DOS APPS ESSENCIAIS ($($ourUpdates.Count))"
            $ListPanel.Controls.Add($divider1)
            
            foreach ($upd in $ourUpdates) {
                $app = $script:Apps | Where-Object { $_.I -eq $upd.I } | Select-Object -First 1
                $extraInfo = "$($upd.CurrentVersion) -> $($upd.NewVersion)"
                $item = New-AppItem -App $upd -ExtraInfo $extraInfo -IsEssential ($app -and $app.E) -Source "update"
                $ListPanel.Controls.Add($item)
            }
        }
        
        if ($systemUpdates.Count -gt 0) {
            $divider2 = New-Divider -Text "OUTRAS ATUALIZACOES DO SISTEMA ($($systemUpdates.Count))"
            $ListPanel.Controls.Add($divider2)
            
            foreach ($upd in $systemUpdates) {
                $extraInfo = "$($upd.CurrentVersion) -> $($upd.NewVersion)"
                $item = New-AppItem -App $upd -ExtraInfo $extraInfo -IsEssential $false -Source "system"
                $ListPanel.Controls.Add($item)
            }
        }
    }
    
    $ListPanel.ResumeLayout($true)
    [System.Windows.Forms.Application]::DoEvents()
    Update-Layout
    & $script:UpdateCount
}

# ============================================
# FUNCAO SHOW-REMOVE-MODE
# ============================================
function Show-RemoveMode {
    $script:CurrentMode = "Remove"
    Set-ViewContext -TitleText "Remover aplicativos" -SubtitleText "Os itens instalados agora ficam agrupados por origem para facilitar triagem e limpeza." -ShowHeader $true
    $WelcomeLabel.Visible = $false
    $SearchBox.Visible = $true
    $ListPanel.Visible = $true
    $ListPanel.Controls.Clear()
    $script:Checkboxes = @()
    $script:AppItems = @()
    
    # Limpar campo de busca
    Set-SearchBoxText -Text $SearchPlaceholder -ForeColor $TextDim
    
    # Mudar sidebar para estado ativo (sem Buscar Online)
    Show-SidebarActive -ShowEssentials $false
    $BtnAction.Text = "REMOVER"
    $BtnAction.BackColor = $Red
    
    Write-Log "Modo REMOVER selecionado" -Type "Info"
    [System.Windows.Forms.Application]::DoEvents()
    
    # Buscar todos os apps categorizados
    $categorizedApps = Get-AllInstalledAppsCategorized
    
    # Armazenar para uso posterior na remocao
    $script:InstalledApps = @()
    $script:InstalledApps += $categorizedApps.Winget
    $script:InstalledApps += $categorizedApps.Store
    $script:InstalledApps += $categorizedApps.UWP
    $script:InstalledApps += $categorizedApps.Local
    
    $ListPanel.SuspendLayout()
    
    $totalApps = $categorizedApps.Winget.Count + $categorizedApps.Store.Count + $categorizedApps.UWP.Count + $categorizedApps.Local.Count
    
    if ($totalApps -eq 0) {
        $noApps = New-Object System.Windows.Forms.Label
        $noApps.Text = "Nenhum app encontrado"
        $noApps.Font = New-Object System.Drawing.Font("Segoe UI", 12)
        $noApps.ForeColor = $TextDim
        $noApps.AutoSize = $true
        $noApps.Margin = New-Object System.Windows.Forms.Padding(20)
        $ListPanel.Controls.Add($noApps)
    } else {
        # Categoria 1: WINGET
        if ($categorizedApps.Winget.Count -gt 0) {
            $ListPanel.Controls.Add((New-Divider -Text "APLICATIVOS WINGET ($($categorizedApps.Winget.Count))"))
        }
        foreach ($app in $categorizedApps.Winget) {
            $extraInfo = if ($app.Version) { "v$($app.Version)" } else { "" }
            $item = New-AppItem -App $app -ExtraInfo $extraInfo -IsEssential $false -Source "Winget"
            $ListPanel.Controls.Add($item)
        }
        
        # Categoria 2: MICROSOFT STORE
        if ($categorizedApps.Store.Count -gt 0) {
            $ListPanel.Controls.Add((New-Divider -Text "APLICATIVOS MICROSOFT STORE ($($categorizedApps.Store.Count))"))
        }
        foreach ($app in $categorizedApps.Store) {
            $extraInfo = if ($app.Version) { "v$($app.Version)" } else { "" }
            $item = New-AppItem -App $app -ExtraInfo $extraInfo -IsEssential $false -Source "Store"
            $ListPanel.Controls.Add($item)
        }
        
        # Categoria 3: WINDOWS APPS / UWP
        if ($categorizedApps.UWP.Count -gt 0) {
            $ListPanel.Controls.Add((New-Divider -Text "APLICATIVOS WINDOWS / UWP ($($categorizedApps.UWP.Count))"))
        }
        foreach ($app in $categorizedApps.UWP) {
            $extraInfo = if ($app.Version) { "v$($app.Version)" } else { "" }
            $item = New-AppItem -App $app -ExtraInfo $extraInfo -IsEssential $false -Source "UWP"
            $ListPanel.Controls.Add($item)
        }
        
        # Categoria 4: PROGRAMAS LOCAIS / EXE-MSI
        if ($categorizedApps.Local.Count -gt 0) {
            $ListPanel.Controls.Add((New-Divider -Text "PROGRAMAS LOCAIS / EXE-MSI ($($categorizedApps.Local.Count))"))
        }
        foreach ($app in $categorizedApps.Local) {
            $extraInfo = if ($app.Version) { "v$($app.Version)" } else { "" }
            $item = New-AppItem -App $app -ExtraInfo $extraInfo -IsEssential $false -Source "Local"
            $ListPanel.Controls.Add($item)
        }
    }
    
    $ListPanel.ResumeLayout($true)
    [System.Windows.Forms.Application]::DoEvents()
    Update-Layout
    & $script:UpdateCount
}

# ============================================
# FUNCAO SHOW-SYSTEM-MODE (Submenu)
# ============================================
function Show-SystemMode {
    $script:CurrentMode = "System"
    Set-ViewContext -TitleText "Ferramentas do sistema" -SubtitleText "Atualizacoes, diagnostico de drivers, ativacao e conta local em um fluxo separado do catalogo." -ShowHeader $true
    $WelcomeLabel.Visible = $false
    $SearchBox.Visible = $false
    $ListPanel.Visible = $false
    $ListPanel.Controls.Clear()
    $script:Checkboxes = @()
    $script:AppItems = @()
    
    # Mostrar submenu Sistema
    Show-SidebarSystem
    
    # Mostrar mensagem de boas-vindas do modo Sistema
    $WelcomeLabel.Text = "Selecione uma opcao: Windows Updates, Drivers, Scripts, Ativador ou Conta Local"
    $WelcomeLabel.Visible = $true
    
    # Ocultar controles de acao
    $LblCount.Visible = $false
    $BtnAction.Visible = $false
    
    Write-Log "Modo SISTEMA selecionado" -Type "Info"
    Update-Layout
}

function Get-AvailableSystemScripts {
    $scripts = @()
    
    $reportPath = Join-Path $script:AppPaths.Temp "RelatorioAmbiente.txt"
    $logsDir = $script:AppPaths.Logs
    $scriptsDir = $script:AppPaths.Scripts
    
    $scripts += @{
        N = "Relatorio do ambiente"
        I = "script.internal.environment.report"
        C = "Interno"
        E = $false
        ScriptType = "Inline"
        ScriptExtension = ".ps1"
        ScriptAction = {
            $ErrorActionPreference = 'Stop'
            $reportDir = $script:AppPaths.Temp
            if (-not (Test-Path $reportDir)) {
                New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
            }
            
            $reportPath = Join-Path $reportDir "RelatorioAmbiente.txt"
            $lines = @()
            $lines += "Relatorio BananaSuisa"
            $lines += "Gerado em: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
            $lines += "Computador: $env:COMPUTERNAME"
            $lines += "Usuario: $env:USERNAME"
            $lines += "PowerShell: $($PSVersionTable.PSVersion)"
            try {
                $wg = Get-BananaSuisaWingetExe
                $wingetVersion = (& $wg --version 2>$null | Out-String).Trim()
            } catch { $wingetVersion = "Nao disponivel" }
            $lines += "Winget: $wingetVersion"
            $lines += "Sistema: $([Environment]::OSVersion.VersionString)"
            Set-Content -Path $reportPath -Value $lines -Encoding UTF8
            Start-Process notepad.exe -ArgumentList $reportPath
        }
    }
    
    $scripts += @{
        N = "Abrir pasta de scripts"
        I = "script.internal.open.scripts.folder"
        C = "Interno"
        E = $false
        ScriptType = "Inline"
        ScriptExtension = ".ps1"
        ScriptAction = {
            $ErrorActionPreference = 'Stop'
            $scriptsDir = $script:AppPaths.Scripts
            try {
                if (-not (Test-Path $scriptsDir)) {
                    New-Item -ItemType Directory -Path $scriptsDir -Force | Out-Null
                }
                Start-Process explorer.exe -ArgumentList $scriptsDir
            } catch {
                throw "Nao foi possivel abrir a pasta de scripts: $($_.Exception.Message)"
            }
        }
    }
    
    $scripts += @{
        N = "Atualizar PowerShell 7"
        I = "script.internal.update.powershell"
        C = "Interno"
        E = $false
        ScriptType = "Inline"
        ScriptExtension = ".ps1"
        ScriptAction = {
            $ErrorActionPreference = 'Stop'
            
            $wgCheck = Get-BananaSuisaWingetExe
            $wingetOk = ($wgCheck -ne "winget" -and (Test-Path -LiteralPath $wgCheck)) -or (Get-Command winget.exe -ErrorAction SilentlyContinue)
            if (-not $wingetOk) {
                throw "Winget nao esta disponivel para instalar ou atualizar o PowerShell."
            }
            
            $beforeVersion = ""
            try { $beforeVersion = (& pwsh --version 2>$null | Out-String).Trim() } catch {}
            
            $wingetArgs = if ([string]::IsNullOrWhiteSpace($beforeVersion)) {
                "install --id Microsoft.PowerShell --exact --source winget --accept-source-agreements --accept-package-agreements --disable-interactivity --silent"
            } else {
                "upgrade --id Microsoft.PowerShell --exact --source winget --accept-source-agreements --accept-package-agreements --disable-interactivity --silent"
            }
            
            $process = Start-Process -FilePath (Get-BananaSuisaWingetExe) -ArgumentList $wingetArgs -Wait -PassThru -WindowStyle Normal
            if ($process.ExitCode -ne 0) {
                throw "Winget retornou codigo $($process.ExitCode) ao atualizar o PowerShell."
            }
            
            $afterVersion = ""
            try { $afterVersion = (& pwsh --version 2>$null | Out-String).Trim() } catch {}
            if ([string]::IsNullOrWhiteSpace($afterVersion)) {
                $afterVersion = "PowerShell instalado/atualizado com sucesso"
            }
            
            [System.Windows.Forms.MessageBox]::Show(
                "Operacao concluida com sucesso.`n`nVersao atual: $afterVersion",
                "PowerShell 7",
                [System.Windows.Forms.MessageBoxButtons]::OK,
                [System.Windows.Forms.MessageBoxIcon]::Information
            ) | Out-Null
        }
    }
    
    if ($scriptsDir -and (Test-Path $scriptsDir)) {
        $fileScripts = Get-ChildItem -Path $scriptsDir -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Extension.ToLower() -in @('.ps1', '.cmd', '.bat', '.exe') } |
            Sort-Object FullName
        
        foreach ($file in $fileScripts) {
            $relativePath = $file.FullName.Substring($scriptsDir.Length).TrimStart('\')
            $scripts += @{
                N = $file.BaseName
                I = "script.file.$($relativePath -replace '[^A-Za-z0-9\.-]', '_')"
                C = "Arquivo"
                E = $false
                ScriptType = "File"
                ScriptPath = $file.FullName
                ScriptExtension = $file.Extension.ToLower()
                RelativePath = $relativePath
            }
        }
    }
    
    return $scripts
}

function Show-ScriptsMode {
    $script:CurrentMode = "Scripts"
    Set-ViewContext -TitleText "Scripts do sistema" -SubtitleText "Execute scripts internos e arquivos salvos na pasta de scripts do BananaSuisa." -ShowHeader $true
    $WelcomeLabel.Visible = $false
    $SearchBox.Visible = $true
    $ListPanel.Visible = $true
    $ListPanel.Controls.Clear()
    $script:Checkboxes = @()
    $script:AppItems = @()
    
    Set-SearchBoxText -Text $SearchPlaceholder -ForeColor $TextDim
    Show-SidebarSystem
    
    $BtnAction.Text = "EXECUTAR"
    $BtnAction.BackColor = [System.Drawing.Color]::FromArgb(129, 199, 132)
    $LblCount.Visible = $true
    $BtnAction.Visible = $true
    
    Write-Log "Modo SCRIPTS selecionado" -Type "Info"
    [System.Windows.Forms.Application]::DoEvents()
    
    $scripts = Get-AvailableSystemScripts
    
    $ListPanel.SuspendLayout()
    if ($scripts.Count -eq 0) {
        $empty = New-Object System.Windows.Forms.Label
        $empty.Text = "Nenhum script disponivel no momento."
        $empty.Font = New-Object System.Drawing.Font("Segoe UI", 12)
        $empty.ForeColor = $TextDim
        $empty.AutoSize = $true
        $empty.Margin = New-Object System.Windows.Forms.Padding(20)
        $ListPanel.Controls.Add($empty)
    } else {
        $internalScripts = @($scripts | Where-Object { $_.ScriptType -eq "Inline" })
        $fileScripts = @($scripts | Where-Object { $_.ScriptType -eq "File" })
        
        if ($internalScripts.Count -gt 0) {
            $ListPanel.Controls.Add((New-Divider -Text "SCRIPTS INTERNOS ($($internalScripts.Count))"))
            foreach ($scriptItem in $internalScripts) {
                $item = New-AppItem -App $scriptItem -ExtraInfo "hard coded" -IsEssential $false -Source "Script"
                $ListPanel.Controls.Add($item)
            }
        }
        
        if ($fileScripts.Count -gt 0) {
            $ListPanel.Controls.Add((New-Divider -Text "SCRIPTS DA PASTA ($($fileScripts.Count))"))
            foreach ($scriptItem in $fileScripts) {
                $extraInfo = if ($scriptItem.RelativePath) { $scriptItem.RelativePath } else { $scriptItem.ScriptExtension }
                $item = New-AppItem -App $scriptItem -ExtraInfo $extraInfo -IsEssential $false -Source "Arquivo"
                $ListPanel.Controls.Add($item)
            }
        }
    }
    $ListPanel.ResumeLayout($true)
    
    Update-Layout
    & $script:UpdateCount
}

# ============================================
# FUNCAO SHOW-PRINTERS MODE
# ============================================
function Show-PrintersMode {
    $script:CurrentMode = "Printers"
    Set-ViewContext -TitleText "Drivers de impressora" -SubtitleText "Selecione o modelo abaixo para abrir o fluxo de download e instalacao assistida." -ShowHeader $true
    $WelcomeLabel.Visible = $false
    $SearchBox.Visible = $false
    $ListPanel.Visible = $false
    $ListPanel.Controls.Clear()
    $script:Checkboxes = @()
    $script:AppItems = @()
    
    # Mostrar submenu Impressoras
    Show-SidebarPrinters
    
    # Mostrar mensagem de boas-vindas do modo Impressoras
    $WelcomeLabel.Text = "Selecione o modelo de impressora para baixar o driver"
    $WelcomeLabel.Visible = $true
    
    # Ocultar controles de acao
    $LblCount.Visible = $false
    $BtnAction.Visible = $false
    
    Write-Log "Modo IMPRESSORAS selecionado" -Type "Info"
    Update-Layout
}

function Show-StorageMode {
    $script:CurrentMode = "Cache"
    Set-ViewContext -TitleText "GERENCIAR INSTALADORES" -SubtitleText "Gerenciar instaladores locais e componentes do sistema"
    $WelcomeLabel.Visible = $true
    $WelcomeLabel.Text = "Selecione a categoria de instaladores no menu lateral"
    $SearchBox.Visible = $false
    $ListPanel.Visible = $false
    
    Show-SidebarStorage
    Update-Layout
}

function Show-CacheMode {
    Show-StorageMode
}

function Show-ManageInstallersMode {
    $script:CurrentMode = "ManageInstallers"
    Set-ViewContext -TitleText "BAIXAR INSTALADORES" -SubtitleText "Selecione os programas para baixar e manter na pasta PacotesBaixados"
    
    $WelcomeLabel.Visible = $false
    $SearchBox.Visible = $true
    $ListPanel.Visible = $true
    $ListPanel.Controls.Clear()
    $script:Checkboxes = @()
    $script:AppItems = @()
    
    Show-SidebarActive -ShowEssentials $false
    
    # Habilitar busca online neste modo
    $BtnSearchOnline.Visible = $true
    
    $ListPanel.SuspendLayout()
    
    # 1. Apps conhecidos da lista
    $shownIds = @()
    foreach ($app in $script:Apps) {
        $item = New-AppItem -App $app -IsEssential $app.E -Source "list" -IsInstalled $false
        $ListPanel.Controls.Add($item)
        $shownIds += $app.I.ToLower()
    }

    # 2. Scannear pasta por arquivos nao listados (IDs desconhecidos)
    if ($script:UseWorkspace -and (Test-Path $script:AppPaths.Installers)) {
        $cacheFiles = Get-ChildItem -Path $script:AppPaths.Installers -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match "^(.+)_(.+)\.(exe|msi|msix|msixbundle|appx|appxbundle|zip)$" }
            
        $extraApps = @()
        foreach ($file in $cacheFiles) {
            if ($file.Name -match "^(.+)_(.+)\.") {
                $foundId = $Matches[1]
                $foundVer = $Matches[2]
                
                if ($shownIds -notcontains $foundId.ToLower()) {
                    $extraApps += @{
                        N = $foundId
                        I = $foundId
                        C = "Pasta Local"
                        E = $false
                        Version = $foundVer
                    }
                    $shownIds += $foundId.ToLower()
                }
            }
        }

        if ($extraApps.Count -gt 0) {
            $ListPanel.Controls.Add((New-Divider -Text "EXTRAPOLADO DA PASTA ($($extraApps.Count))"))
            foreach ($app in $extraApps) {
                $item = New-AppItem -App $app -ExtraInfo "v$($app.Version)" -IsEssential $false -Source "LocalOnly"
                $ListPanel.Controls.Add($item)
            }
        }
    }

    $ListPanel.ResumeLayout($true)
    
    $BtnAction.Text = "BAIXAR PARA PASTA"
    $BtnAction.BackColor = $Green
    $BtnAction.ForeColor = $Text
    
    [System.Windows.Forms.Application]::DoEvents()
    Update-Layout
    & $script:UpdateCount
}

# ============================================
# FUNCAO PARA BAIXAR E INSTALAR DRIVER DE IMPRESSORA
# ============================================
function Install-PrinterDriver {
    param(
        [string]$PrinterName,
        [string]$DownloadUrl,
        [string]$FileName
    )
    
    if ($script:CancelRequested) {
        return @{ Success = $false; ExitCode = -1; Message = "Cancelado" }
    }
    
    # Verificar se a URL foi fornecida
    if ([string]::IsNullOrWhiteSpace($DownloadUrl)) {
        Complete-LogProgress
        Write-Log "URL de download nao fornecida para $PrinterName" -Type "Warning"
        return @{ Success = $false; ExitCode = -1; Message = "URL de download nao disponivel." }
    }

    # Extrair nome do arquivo da URL se FileName nao foi fornecido
    if ([string]::IsNullOrWhiteSpace($FileName)) {
        $FileName = Split-Path -Leaf $DownloadUrl
    }

    # Definir local de armazenamento (Cache ou Temp)
    $storageDir = if ($script:UseWorkspace) { $script:AppPaths.Drivers } else { "$env:TEMP\PrinterDrivers" }
    if (-not (Test-Path $storageDir)) { New-Item -ItemType Directory -Path $storageDir -Force | Out-Null }
    
    $downloadPath = Join-Path $storageDir $FileName
    $isCached = Test-Path $downloadPath

    if ($isCached) {
        Write-Log "Usando driver do cache: $PrinterName" -Type "Info"
    } else {
        Write-Log "Baixando driver: $PrinterName" -Type "Progress"
        Update-LogProgress "$PrinterName" "[Baixando...]"
        Update-Stage "Downloading"
        
        try {
            Write-Log "URL: $DownloadUrl" -Type "Info"
            Invoke-WebDownload -Uri $DownloadUrl -OutFile $downloadPath -LogMessage "$PrinterName"
            Write-Log "Download concluido." -Type "Success"
        } catch {
            Complete-LogProgress
            Write-Log "Erro ao baixar driver $PrinterName : $_" -Type "Error"
            return @{ Success = $false; ExitCode = -1; Message = "Erro no download: $_" }
        }
    }
    
    try {
        # Verificar se e um arquivo ZIP
        if ($FileName -like "*.zip") {
            Update-LogProgress "$PrinterName" "[Extraindo...]"
            Update-Stage "Extracting"
            
            # Extrair sempre em pasta temporaria para evitar poluir o cache com pastas
            $extractPath = Join-Path "$env:TEMP\PrinterDrivers_Extract" ($PrinterName -replace '[^\w\-]', '_')
            if (Test-Path $extractPath) { Remove-Item $extractPath -Recurse -Force }
            
            Expand-Archive -Path $downloadPath -DestinationPath $extractPath -Force
            
            # Abrir a pasta extraida para o usuario
            Start-Process explorer.exe -ArgumentList $extractPath
            
            Complete-LogProgress
            Write-Log "Driver $PrinterName extraido em: $extractPath" -Type "Success"
            return @{ Success = $true; ExitCode = 0; Message = "Arquivo extraido em: $extractPath" }
        }
        
        Update-LogProgress "$PrinterName" "[Instalando...]"
        Update-Stage "Installing"
        
        # Executar o instalador - tentar diferentes metodos
        $process = $null
        $exitCode = $null
        
        # Tentar com /S (silent) primeiro
        try {
            $process = Start-Process -FilePath $downloadPath -ArgumentList "/S" -PassThru -NoNewWindow -ErrorAction Stop
            while (-not $process.HasExited) {
                [System.Windows.Forms.Application]::DoEvents()
                Start-Sleep -Milliseconds 100
            }
            $exitCode = $process.ExitCode
        } catch {
            # Se falhar, tentar executar normalmente
            Write-Log "Tentando instalacao interativa..." -Type "Info"
            $process = Start-Process -FilePath $downloadPath -PassThru -ErrorAction Stop
            while (-not $process.HasExited) {
                [System.Windows.Forms.Application]::DoEvents()
                Start-Sleep -Milliseconds 100
            }
            $exitCode = $process.ExitCode
        }
        
        if ($exitCode -eq 0 -or $null -eq $exitCode) {
            Complete-LogProgress
            Write-Log "Driver $PrinterName instalado com sucesso" -Type "Success"
            return @{ Success = $true; ExitCode = 0; Message = "Driver instalado com sucesso" }
        } else {
            Complete-LogProgress
            Write-Log "Erro ao instalar driver $PrinterName. Codigo de saida: $exitCode" -Type "Error"
            return @{ Success = $false; ExitCode = $exitCode; Message = "Erro na instalacao (codigo: $exitCode)" }
        }
    } catch {
        Complete-LogProgress
        Write-Log "Erro ao instalar driver $PrinterName : $_" -Type "Error"
        return @{ Success = $false; ExitCode = -1; Message = "Erro: $_" }
    }
}

# ============================================
# FUNCAO SHOW-ACTIVATOR (Microsoft Activation Scripts)
# ============================================
function Show-ActivatorMode {
    $script:CurrentMode = "Activator"
    $WelcomeLabel.Visible = $false
    $SearchBox.Visible = $false
    $ListPanel.Visible = $true
    $ListPanel.Controls.Clear()
    $script:Checkboxes = @()
    $script:AppItems = @()
    
    # Cores locais (copia das variaveis de script)
    $localTextMain = [System.Drawing.Color]::FromArgb(230, 230, 230)
    $localTextDim = [System.Drawing.Color]::FromArgb(140, 140, 140)
    $localGreen = [System.Drawing.Color]::FromArgb(76, 175, 80)
    $localBlue = [System.Drawing.Color]::FromArgb(33, 150, 243)
    $localYellow = [System.Drawing.Color]::FromArgb(255, 193, 7)
    $localCardBg = [System.Drawing.Color]::FromArgb(45, 45, 48)
    $localPurple = [System.Drawing.Color]::FromArgb(138, 43, 226)
    
    # Ocultar menu principal e mostrar voltar
    $BtnModeInstall.Visible = $false
    $BtnModeUpdate.Visible = $false
    $BtnModeRemove.Visible = $false
    $BtnModeSystem.Visible = $false
    $BtnBack.Visible = $true
    
    # Ocultar outros botoes da sidebar (incluindo submenu Sistema)
    $BtnAll.Visible = $false
    $BtnNone.Visible = $false
    $BtnSearchOnline.Visible = $false
    $BtnWinUpdates.Visible = $false
    $BtnDrivers.Visible = $false
    $BtnActivator.Visible = $false
    $BtnLocalAccount.Visible = $false
    $BtnScripts.Visible = $false
    $BtnScripts.Visible = $false
    
    # Ocultar controles de acao padrao
    $LblCount.Visible = $false
    $BtnAction.Visible = $false
    
    # Destacar botao ativo
    $BtnActivator.BackColor = $localPurple
    
    # === CRIAR INTERFACE DO ATIVADOR ===
    
    # Titulo
    $TitleLabel = New-Object System.Windows.Forms.Label
    $TitleLabel.Text = "Microsoft Activation Scripts (MAS)"
    $TitleLabel.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 14)
    $TitleLabel.ForeColor = $localPurple
    $TitleLabel.AutoSize = $true
    $TitleLabel.Margin = New-Object System.Windows.Forms.Padding(5, 10, 5, 5)
    $ListPanel.Controls.Add($TitleLabel)
    
    # Descricao
    $DescLabel = New-Object System.Windows.Forms.Label
    $DescLabel.Text = "Ferramenta open-source para ativacao do Windows e Office.`nMetodos: HWID, Ohook, TSforge e KMS Online."
    $DescLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10)
    $DescLabel.ForeColor = $localTextMain
    $DescLabel.AutoSize = $true
    $DescLabel.Margin = New-Object System.Windows.Forms.Padding(5, 5, 5, 15)
    $ListPanel.Controls.Add($DescLabel)
    
    # Aviso
    $WarningLabel = New-Object System.Windows.Forms.Label
    $WarningLabel.Text = "AVISO: O script sera baixado diretamente do repositorio oficial.`nFonte: github.com/massgravel/Microsoft-Activation-Scripts"
    $WarningLabel.Font = New-Object System.Drawing.Font("Segoe UI", 9)
    $WarningLabel.ForeColor = $localYellow
    $WarningLabel.AutoSize = $true
    $WarningLabel.Margin = New-Object System.Windows.Forms.Padding(5, 5, 5, 20)
    $ListPanel.Controls.Add($WarningLabel)
    
    # === BOTOES DE OPCAO ===
    
    # Botao principal - Executar MAS (menu interativo)
    $BtnRunMAS = New-Object System.Windows.Forms.Button
    $BtnRunMAS.Text = "Abrir Menu MAS (Recomendado)"
    $BtnRunMAS.Size = New-Object System.Drawing.Size(280, 45)
    $BtnRunMAS.Margin = New-Object System.Windows.Forms.Padding(5, 5, 5, 10)
    $BtnRunMAS.FlatStyle = "Flat"
    $BtnRunMAS.FlatAppearance.BorderColor = $localPurple
    $BtnRunMAS.BackColor = $localCardBg
    $BtnRunMAS.ForeColor = $localPurple
    $BtnRunMAS.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 10)
    $BtnRunMAS.Cursor = "Hand"
    $BtnRunMAS.Add_Click({
        $confirmResult = [System.Windows.Forms.MessageBox]::Show(
            "Deseja abrir o Microsoft Activation Scripts?`n`nIsso abrira uma nova janela do PowerShell com privilegios de administrador.",
            "Confirmar",
            [System.Windows.Forms.MessageBoxButtons]::YesNo,
            [System.Windows.Forms.MessageBoxIcon]::Question
        )
        if ($confirmResult -eq [System.Windows.Forms.DialogResult]::Yes) {
            Write-Log "Iniciando Microsoft Activation Scripts..." -Type "Info"
            try {
                $command = "irm https://get.activated.win | iex"
                Start-Process powershell -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", $command -Verb RunAs
                Write-Log "MAS iniciado em nova janela" -Type "Success"
            } catch {
                Write-Log "Erro ao iniciar MAS: $_" -Type "Error"
                [System.Windows.Forms.MessageBox]::Show(
                    "Erro ao iniciar o Microsoft Activation Scripts:`n$_",
                    "Erro",
                    [System.Windows.Forms.MessageBoxButtons]::OK,
                    [System.Windows.Forms.MessageBoxIcon]::Error
                )
            }
        }
    })
    $ListPanel.Controls.Add($BtnRunMAS)
    
    # Info sobre o menu
    $InfoLabel1 = New-Object System.Windows.Forms.Label
    $InfoLabel1.Text = "O menu interativo permite escolher entre:`n  [1] HWID - Ativacao permanente do Windows`n  [2] Ohook - Ativacao permanente do Office`n  [3] KMS38 - Ativacao ate 2038`n  [4] KMS Online - Ativacao temporaria (180 dias)`n  [5] Solucao de problemas"
    $InfoLabel1.Font = New-Object System.Drawing.Font("Consolas", 9)
    $InfoLabel1.ForeColor = $localTextDim
    $InfoLabel1.AutoSize = $true
    $InfoLabel1.Margin = New-Object System.Windows.Forms.Padding(5, 5, 5, 20)
    $ListPanel.Controls.Add($InfoLabel1)
    
    # Separador
    $SepPanel = New-Object System.Windows.Forms.Panel
    $SepPanel.Size = New-Object System.Drawing.Size(300, 1)
    $SepPanel.BackColor = $localTextDim
    $SepPanel.Margin = New-Object System.Windows.Forms.Padding(5, 10, 5, 15)
    $ListPanel.Controls.Add($SepPanel)
    
    # Label de atalhos
    $ShortcutsLabel = New-Object System.Windows.Forms.Label
    $ShortcutsLabel.Text = "Atalhos Rapidos (execucao direta):"
    $ShortcutsLabel.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 10)
    $ShortcutsLabel.ForeColor = $localTextMain
    $ShortcutsLabel.AutoSize = $true
    $ShortcutsLabel.Margin = New-Object System.Windows.Forms.Padding(5, 5, 5, 10)
    $ListPanel.Controls.Add($ShortcutsLabel)
    
    # Botao HWID - Windows
    $BtnHWID = New-Object System.Windows.Forms.Button
    $BtnHWID.Text = "HWID - Ativar Windows (Permanente)"
    $BtnHWID.Size = New-Object System.Drawing.Size(280, 38)
    $BtnHWID.Margin = New-Object System.Windows.Forms.Padding(5, 5, 5, 5)
    $BtnHWID.FlatStyle = "Flat"
    $BtnHWID.FlatAppearance.BorderColor = $localGreen
    $BtnHWID.BackColor = $localCardBg
    $BtnHWID.ForeColor = $localGreen
    $BtnHWID.Font = New-Object System.Drawing.Font("Segoe UI", 9)
    $BtnHWID.Cursor = "Hand"
    $BtnHWID.Add_Click({
        $confirmResult = [System.Windows.Forms.MessageBox]::Show(
            "Deseja executar a ativacao HWID do Windows?`n`nIsso abrira uma nova janela do PowerShell com privilegios de administrador.",
            "Confirmar",
            [System.Windows.Forms.MessageBoxButtons]::YesNo,
            [System.Windows.Forms.MessageBoxIcon]::Question
        )
        if ($confirmResult -eq [System.Windows.Forms.DialogResult]::Yes) {
            Write-Log "Executando ativacao HWID..." -Type "Info"
            try {
                $command = "irm https://get.activated.win | iex"
                Start-Process powershell -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", $command -Verb RunAs
                Write-Log "HWID iniciado" -Type "Success"
            } catch {
                Write-Log "Erro: $_" -Type "Error"
            }
        }
    })
    $ListPanel.Controls.Add($BtnHWID)
    
    # Botao Ohook - Office
    $BtnOhook = New-Object System.Windows.Forms.Button
    $BtnOhook.Text = "Ohook - Ativar Office (Permanente)"
    $BtnOhook.Size = New-Object System.Drawing.Size(280, 38)
    $BtnOhook.Margin = New-Object System.Windows.Forms.Padding(5, 5, 5, 5)
    $BtnOhook.FlatStyle = "Flat"
    $BtnOhook.FlatAppearance.BorderColor = $localBlue
    $BtnOhook.BackColor = $localCardBg
    $BtnOhook.ForeColor = $localBlue
    $BtnOhook.Font = New-Object System.Drawing.Font("Segoe UI", 9)
    $BtnOhook.Cursor = "Hand"
    $BtnOhook.Add_Click({
        $confirmResult = [System.Windows.Forms.MessageBox]::Show(
            "Deseja executar a ativacao Ohook do Office?`n`nIsso abrira uma nova janela do PowerShell com privilegios de administrador.",
            "Confirmar",
            [System.Windows.Forms.MessageBoxButtons]::YesNo,
            [System.Windows.Forms.MessageBoxIcon]::Question
        )
        if ($confirmResult -eq [System.Windows.Forms.DialogResult]::Yes) {
            Write-Log "Executando ativacao Ohook..." -Type "Info"
            try {
                $command = "irm https://get.activated.win | iex"
                Start-Process powershell -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", $command -Verb RunAs
                Write-Log "Ohook iniciado" -Type "Success"
            } catch {
                Write-Log "Erro: $_" -Type "Error"
            }
        }
    })
    $ListPanel.Controls.Add($BtnOhook)
    
    # Botao Troubleshoot
    $BtnTroubleshoot = New-Object System.Windows.Forms.Button
    $BtnTroubleshoot.Text = "Solucionar Problemas de Ativacao"
    $BtnTroubleshoot.Size = New-Object System.Drawing.Size(280, 38)
    $BtnTroubleshoot.Margin = New-Object System.Windows.Forms.Padding(5, 5, 5, 5)
    $BtnTroubleshoot.FlatStyle = "Flat"
    $BtnTroubleshoot.FlatAppearance.BorderColor = $localYellow
    $BtnTroubleshoot.BackColor = $localCardBg
    $BtnTroubleshoot.ForeColor = $localYellow
    $BtnTroubleshoot.Font = New-Object System.Drawing.Font("Segoe UI", 9)
    $BtnTroubleshoot.Cursor = "Hand"
    $BtnTroubleshoot.Add_Click({
        $confirmResult = [System.Windows.Forms.MessageBox]::Show(
            "Deseja abrir a solucao de problemas de ativacao?`n`nIsso abrira uma nova janela do PowerShell com privilegios de administrador.",
            "Confirmar",
            [System.Windows.Forms.MessageBoxButtons]::YesNo,
            [System.Windows.Forms.MessageBoxIcon]::Question
        )
        if ($confirmResult -eq [System.Windows.Forms.DialogResult]::Yes) {
            Write-Log "Abrindo solucao de problemas..." -Type "Info"
            try {
                $command = "irm https://get.activated.win | iex"
                Start-Process powershell -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", $command -Verb RunAs
                Write-Log "Troubleshoot iniciado" -Type "Success"
            } catch {
                Write-Log "Erro: $_" -Type "Error"
            }
        }
    })
    $ListPanel.Controls.Add($BtnTroubleshoot)
    
    # Link para o GitHub
    $LinkLabel = New-Object System.Windows.Forms.LinkLabel
    $LinkLabel.Text = "Visitar repositorio oficial no GitHub"
    $LinkLabel.Font = New-Object System.Drawing.Font("Segoe UI", 9)
    $LinkLabel.LinkColor = [System.Drawing.Color]::FromArgb(100, 149, 237)
    $LinkLabel.ActiveLinkColor = $localPurple
    $LinkLabel.AutoSize = $true
    $LinkLabel.Margin = New-Object System.Windows.Forms.Padding(5, 20, 5, 5)
    $LinkLabel.Add_Click({
        Start-Process "https://github.com/massgravel/Microsoft-Activation-Scripts"
    })
    $ListPanel.Controls.Add($LinkLabel)
    
    # Link para documentacao
    $LinkLabel2 = New-Object System.Windows.Forms.LinkLabel
    $LinkLabel2.Text = "Documentacao completa (massgrave.dev)"
    $LinkLabel2.Font = New-Object System.Drawing.Font("Segoe UI", 9)
    $LinkLabel2.LinkColor = [System.Drawing.Color]::FromArgb(100, 149, 237)
    $LinkLabel2.ActiveLinkColor = $localPurple
    $LinkLabel2.AutoSize = $true
    $LinkLabel2.Margin = New-Object System.Windows.Forms.Padding(5, 5, 5, 5)
    $LinkLabel2.Add_Click({
        Start-Process "https://massgrave.dev/"
    })
    $ListPanel.Controls.Add($LinkLabel2)
    
    Write-Log "Modo ATIVADOR selecionado" -Type "Info"
    Update-Layout
}

# ============================================
# FUNCAO SHOW-LOCAL-ACCOUNT (Converter Conta MS para Local)
# ============================================
function Show-LocalAccountMode {
    $script:CurrentMode = "LocalAccount"
    $WelcomeLabel.Visible = $false
    $SearchBox.Visible = $false
    $ListPanel.Visible = $true
    $ListPanel.Controls.Clear()
    $script:Checkboxes = @()
    $script:AppItems = @()
    
    # Cores locais
    $localTextMain = [System.Drawing.Color]::FromArgb(230, 230, 230)
    $localTextDim = [System.Drawing.Color]::FromArgb(140, 140, 140)
    $localGreen = [System.Drawing.Color]::FromArgb(76, 175, 80)
    $localRed = [System.Drawing.Color]::FromArgb(244, 67, 54)
    $localYellow = [System.Drawing.Color]::FromArgb(255, 193, 7)
    $localCardBg = [System.Drawing.Color]::FromArgb(45, 45, 48)
    $localPink = [System.Drawing.Color]::FromArgb(236, 72, 153)
    
    # Ocultar menu principal e mostrar voltar
    $BtnModeInstall.Visible = $false
    $BtnModeUpdate.Visible = $false
    $BtnModeRemove.Visible = $false
    $BtnModeSystem.Visible = $false
    $BtnBack.Visible = $true
    
    # Ocultar outros botoes da sidebar (incluindo submenu Sistema)
    $BtnAll.Visible = $false
    $BtnNone.Visible = $false
    $BtnSearchOnline.Visible = $false
    $BtnWinUpdates.Visible = $false
    $BtnDrivers.Visible = $false
    $BtnActivator.Visible = $false
    $BtnLocalAccount.Visible = $false
    $BtnScripts.Visible = $false
    $BtnScripts.Visible = $false
    
    # Ocultar controles de acao padrao
    $LblCount.Visible = $false
    $BtnAction.Visible = $false
    
    # Destacar botao ativo
    $BtnLocalAccount.BackColor = $localPink
    
    # === CRIAR INTERFACE ===
    
    # Titulo
    $TitleLabel = New-Object System.Windows.Forms.Label
    $TitleLabel.Text = "Converter para Conta Local"
    $TitleLabel.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 14)
    $TitleLabel.ForeColor = $localPink
    $TitleLabel.AutoSize = $true
    $TitleLabel.Margin = New-Object System.Windows.Forms.Padding(5, 10, 5, 5)
    $ListPanel.Controls.Add($TitleLabel)
    
    # Descricao
    $DescLabel = New-Object System.Windows.Forms.Label
    $DescLabel.Text = "Remove a vinculacao com a conta Microsoft e cria uma conta local`ncom o nome do perfil selecionado. A nova conta sera Administrador."
    $DescLabel.Font = New-Object System.Drawing.Font("Segoe UI", 10)
    $DescLabel.ForeColor = $localTextMain
    $DescLabel.AutoSize = $true
    $DescLabel.Margin = New-Object System.Windows.Forms.Padding(5, 5, 5, 15)
    $ListPanel.Controls.Add($DescLabel)
    
    # Aviso
    $WarningLabel = New-Object System.Windows.Forms.Label
    $WarningLabel.Text = "ATENCAO: Este processo ira:`n  - Desconectar a conta Microsoft atual`n  - Criar uma nova conta local com o nome selecionado`n  - A nova conta tera privilegios de Administrador`n  - Sera necessario reiniciar o computador"
    $WarningLabel.Font = New-Object System.Drawing.Font("Segoe UI", 9)
    $WarningLabel.ForeColor = $localYellow
    $WarningLabel.AutoSize = $true
    $WarningLabel.Margin = New-Object System.Windows.Forms.Padding(5, 5, 5, 20)
    $ListPanel.Controls.Add($WarningLabel)
    
    # === SELECAO DE PERFIL ===
    
    $ProfileLabel = New-Object System.Windows.Forms.Label
    $ProfileLabel.Text = "Selecione o nome da conta local:"
    $ProfileLabel.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 10)
    $ProfileLabel.ForeColor = $localTextMain
    $ProfileLabel.AutoSize = $true
    $ProfileLabel.Margin = New-Object System.Windows.Forms.Padding(5, 5, 5, 10)
    $ListPanel.Controls.Add($ProfileLabel)
    
    # Lista de perfis disponiveis (nomes de usuario)
    $userProfiles = @("Caixa", "Retaguarda", "Escritorio", "TI", "Desenvolvedor", "Admin", "Usuario")
    
    # Criar ComboBox para selecao
    $ComboProfile = New-Object System.Windows.Forms.ComboBox
    $ComboProfile.Size = New-Object System.Drawing.Size(280, 30)
    $ComboProfile.Margin = New-Object System.Windows.Forms.Padding(5, 5, 5, 15)
    $ComboProfile.Font = New-Object System.Drawing.Font("Segoe UI", 10)
    $ComboProfile.DropDownStyle = "DropDownList"
    $ComboProfile.BackColor = $localCardBg
    $ComboProfile.ForeColor = $localTextMain
    foreach ($prof in $userProfiles) {
        $ComboProfile.Items.Add($prof) | Out-Null
    }
    $ComboProfile.SelectedIndex = 0
    $ListPanel.Controls.Add($ComboProfile)
    
    # Campo de senha (opcional)
    $PasswordLabel = New-Object System.Windows.Forms.Label
    $PasswordLabel.Text = "Senha (deixe em branco para sem senha):"
    $PasswordLabel.Font = New-Object System.Drawing.Font("Segoe UI", 9)
    $PasswordLabel.ForeColor = $localTextDim
    $PasswordLabel.AutoSize = $true
    $PasswordLabel.Margin = New-Object System.Windows.Forms.Padding(5, 5, 5, 5)
    $ListPanel.Controls.Add($PasswordLabel)
    
    $TxtPassword = New-Object System.Windows.Forms.TextBox
    $TxtPassword.Size = New-Object System.Drawing.Size(280, 26)
    $TxtPassword.Margin = New-Object System.Windows.Forms.Padding(5, 5, 5, 20)
    $TxtPassword.Font = New-Object System.Drawing.Font("Segoe UI", 10)
    $TxtPassword.BackColor = $localCardBg
    $TxtPassword.ForeColor = $localTextMain
    $TxtPassword.UseSystemPasswordChar = $true
    $ListPanel.Controls.Add($TxtPassword)
    
    # Separador
    $SepPanel = New-Object System.Windows.Forms.Panel
    $SepPanel.Size = New-Object System.Drawing.Size(300, 1)
    $SepPanel.BackColor = $localTextDim
    $SepPanel.Margin = New-Object System.Windows.Forms.Padding(5, 10, 5, 15)
    $ListPanel.Controls.Add($SepPanel)
    
    # Botao Criar Conta Local
    $BtnCreateAccount = New-Object System.Windows.Forms.Button
    $BtnCreateAccount.Text = "Criar Conta Local e Desconectar MS"
    $BtnCreateAccount.Size = New-Object System.Drawing.Size(280, 45)
    $BtnCreateAccount.Margin = New-Object System.Windows.Forms.Padding(5, 5, 5, 10)
    $BtnCreateAccount.FlatStyle = "Flat"
    $BtnCreateAccount.FlatAppearance.BorderColor = $localPink
    $BtnCreateAccount.BackColor = $localCardBg
    $BtnCreateAccount.ForeColor = $localPink
    $BtnCreateAccount.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 10)
    $BtnCreateAccount.Cursor = "Hand"
    $BtnCreateAccount.Tag = @{ Combo = $ComboProfile; Password = $TxtPassword }
    $BtnCreateAccount.Add_Click({
        $combo = $this.Tag.Combo
        $pwdBox = $this.Tag.Password
        $selectedProfile = $combo.SelectedItem.ToString()
        $password = $pwdBox.Text
        
        $confirmResult = [System.Windows.Forms.MessageBox]::Show(
            "Voce esta prestes a criar uma conta local chamada '$selectedProfile' com privilegios de Administrador.`n`nDeseja continuar?`n`nNOTA: Sera necessario reiniciar o computador apos o processo.",
            "Confirmar Criacao de Conta",
            [System.Windows.Forms.MessageBoxButtons]::YesNo,
            [System.Windows.Forms.MessageBoxIcon]::Warning
        )
        
        if ($confirmResult -eq [System.Windows.Forms.DialogResult]::Yes) {
            Write-Log "Criando conta local: $selectedProfile" -Type "Info"
            try {
                # Criar conta local com privilegios de administrador
                $securePassword = $null
                if ($password -ne "") {
                    $securePassword = ConvertTo-SecureString $password -AsPlainText -Force
                }
                
                # Verificar se a conta ja existe
                $existingUser = Get-LocalUser -Name $selectedProfile -ErrorAction SilentlyContinue
                if ($existingUser) {
                    [System.Windows.Forms.MessageBox]::Show(
                        "Ja existe uma conta com o nome '$selectedProfile'.`nEscolha outro nome ou remova a conta existente primeiro.",
                        "Conta Existente",
                        [System.Windows.Forms.MessageBoxButtons]::OK,
                        [System.Windows.Forms.MessageBoxIcon]::Warning
                    )
                    Write-Log "Conta '$selectedProfile' ja existe" -Type "Warning"
                    return
                }
                
                # Criar nova conta local
                if ($securePassword) {
                    New-LocalUser -Name $selectedProfile -Password $securePassword -FullName $selectedProfile -Description "Conta local criada pelo WingetAppInstaller" -PasswordNeverExpires -ErrorAction Stop
                } else {
                    New-LocalUser -Name $selectedProfile -NoPassword -FullName $selectedProfile -Description "Conta local criada pelo WingetAppInstaller" -PasswordNeverExpires -ErrorAction Stop
                }
                
                # Adicionar ao grupo Administradores
                Add-LocalGroupMember -Group "Administradores" -Member $selectedProfile -ErrorAction SilentlyContinue
                # Tentar tambem com nome em ingles (para sistemas em ingles)
                Add-LocalGroupMember -Group "Administrators" -Member $selectedProfile -ErrorAction SilentlyContinue
                
                Write-Log "Conta '$selectedProfile' criada com sucesso!" -Type "Success"
                
                $restartResult = [System.Windows.Forms.MessageBox]::Show(
                    "Conta '$selectedProfile' criada com sucesso!`n`nA conta foi adicionada ao grupo Administradores.`n`nDeseja reiniciar o computador agora para fazer login com a nova conta?",
                    "Conta Criada",
                    [System.Windows.Forms.MessageBoxButtons]::YesNo,
                    [System.Windows.Forms.MessageBoxIcon]::Information
                )
                
                if ($restartResult -eq [System.Windows.Forms.DialogResult]::Yes) {
                    Write-Log "Reiniciando computador..." -Type "Info"
                    Restart-Computer -Force
                }
                
            } catch {
                Write-Log "Erro ao criar conta: $_" -Type "Error"
                [System.Windows.Forms.MessageBox]::Show(
                    "Erro ao criar conta local:`n$_`n`nCertifique-se de executar o script como Administrador.",
                    "Erro",
                    [System.Windows.Forms.MessageBoxButtons]::OK,
                    [System.Windows.Forms.MessageBoxIcon]::Error
                )
            }
        }
    })
    $ListPanel.Controls.Add($BtnCreateAccount)
    
    # Info adicional
    $InfoLabel = New-Object System.Windows.Forms.Label
    $InfoLabel.Text = "Apos criar a conta, faca logout e entre com a nova conta.`nVoce pode remover a conta Microsoft antigo posteriormente`nno Painel de Controle > Contas de Usuario."
    $InfoLabel.Font = New-Object System.Drawing.Font("Segoe UI", 9)
    $InfoLabel.ForeColor = $localTextDim
    $InfoLabel.AutoSize = $true
    $InfoLabel.Margin = New-Object System.Windows.Forms.Padding(5, 10, 5, 5)
    $ListPanel.Controls.Add($InfoLabel)
    
    Write-Log "Modo CONTA LOCAL selecionado" -Type "Info"
    Update-Layout
}

# ============================================
# FUNCAO SHOW-WINDOWS-UPDATES
# ============================================
function Show-WindowsUpdatesMode {
    $script:CurrentMode = "WindowsUpdates"
    $WelcomeLabel.Visible = $false
    $SearchBox.Visible = $true
    $ListPanel.Visible = $true
    $ListPanel.Controls.Clear()
    $script:Checkboxes = @()
    $script:AppItems = @()
    
    # Limpar campo de busca
    Set-SearchBoxText -Text $SearchPlaceholder -ForeColor $TextDim
    
    $BtnAction.Text = "INSTALAR"
    $BtnAction.BackColor = $Blue
    $LblCount.Visible = $true
    $BtnAction.Visible = $true
    
    Write-Log "Modo WINDOWS UPDATES selecionado" -Type "Info"
    [System.Windows.Forms.Application]::DoEvents()
    
    # Buscar atualizacoes
    $updates = Get-PendingWindowsUpdates
    
    $ListPanel.SuspendLayout()
    
    if ($updates.Count -eq 0) {
        $noUpdates = New-Object System.Windows.Forms.Label
        $noUpdates.Text = "Nenhuma atualizacao pendente encontrada!"
        $noUpdates.Font = New-Object System.Drawing.Font("Segoe UI", 12)
        $noUpdates.ForeColor = $Green
        $noUpdates.AutoSize = $true
        $noUpdates.Margin = New-Object System.Windows.Forms.Padding(20)
        $ListPanel.Controls.Add($noUpdates)
    } else {
        foreach ($update in $updates) {
            $extraInfo = if ($update.Version) { $update.Version } else { "" }
            $cat = if ($update.Category) { $update.Category } else { "Update" }
            $item = New-AppItem -App $update -ExtraInfo $extraInfo -IsEssential $false -Source $cat
            $ListPanel.Controls.Add($item)
        }
    }
    
    $ListPanel.ResumeLayout($true)
    [System.Windows.Forms.Application]::DoEvents()
    Update-Layout
    & $script:UpdateCount
}

# ============================================
# FUNCAO SHOW-MISSING-DRIVERS
# ============================================
function Show-MissingDriversMode {
    $script:CurrentMode = "Drivers"
    $WelcomeLabel.Visible = $false
    $SearchBox.Visible = $true
    $ListPanel.Visible = $true
    $ListPanel.Controls.Clear()
    $script:Checkboxes = @()
    $script:AppItems = @()
    
    # Limpar campo de busca
    Set-SearchBoxText -Text $SearchPlaceholder -ForeColor $TextDim
    
    $BtnAction.Text = "ATUALIZAR"
    $BtnAction.BackColor = [System.Drawing.Color]::FromArgb(255, 180, 100)
    $LblCount.Visible = $true
    $BtnAction.Visible = $true
    
    Write-Log "Modo DRIVERS selecionado" -Type "Info"
    [System.Windows.Forms.Application]::DoEvents()
    
    # Buscar drivers
    $drivers = Get-MissingDrivers
    
    $ListPanel.SuspendLayout()
    
    if ($drivers.Count -eq 0) {
        $noDrivers = New-Object System.Windows.Forms.Label
        $noDrivers.Text = "Nenhum problema de driver encontrado!"
        $noDrivers.Font = New-Object System.Drawing.Font("Segoe UI", 12)
        $noDrivers.ForeColor = $Green
        $noDrivers.AutoSize = $true
        $noDrivers.Margin = New-Object System.Windows.Forms.Padding(20)
        $ListPanel.Controls.Add($noDrivers)
    } else {
        # Separar dispositivos com problema e drivers disponiveis
        $problemDevices = $drivers | Where-Object { $_.ErrorCode -ne 0 }
        $availableDrivers = $drivers | Where-Object { $_.ErrorCode -eq 0 }
        
        # Dispositivos com problema
        if ($problemDevices.Count -gt 0) {
            foreach ($device in $problemDevices) {
                $extraInfo = if ($device.Version) { $device.Version } else { "" }
                $cat = if ($device.Category) { $device.Category } else { "Device" }
                $item = New-AppItem -App $device -ExtraInfo $extraInfo -IsEssential $false -Source "Problema"
                $ListPanel.Controls.Add($item)
            }
        }
        
        # Drivers disponiveis via Windows Update
        if ($availableDrivers.Count -gt 0) {
            foreach ($driver in $availableDrivers) {
                $extraInfo = if ($driver.Version) { $driver.Version } else { "" }
                $item = New-AppItem -App $driver -ExtraInfo $extraInfo -IsEssential $false -Source "Disponivel"
                $ListPanel.Controls.Add($item)
            }
        }
    }
    
    $ListPanel.ResumeLayout($true)
    [System.Windows.Forms.Application]::DoEvents()
    Update-Layout
    & $script:UpdateCount
}

function Invoke-SystemScript {
    param([hashtable]$ScriptItem)
    
    if (-not $ScriptItem) {
        return @{ Success = $false; ExitCode = -1; Message = "Script invalido" }
    }
    
    $scriptName = if ($ScriptItem.N) { $ScriptItem.N } else { "Script" }
    $scriptType = if ($ScriptItem.ScriptType) { $ScriptItem.ScriptType } else { "File" }
    
    try {
        if ($scriptType -eq "Inline") {
            Write-FileLog "Invoke-SystemScript: Executando script interno '$scriptName'" "INFO"
            Update-LogProgress "$scriptName" "[Executando script interno...]"
            Update-Stage "Installing"
            
            if ($ScriptItem.ScriptAction -is [scriptblock]) {
                & $ScriptItem.ScriptAction
            } elseif (-not [string]::IsNullOrWhiteSpace($ScriptItem.ScriptContent)) {
                & ([scriptblock]::Create($ScriptItem.ScriptContent))
            } else {
                return @{ Success = $false; ExitCode = -1; Message = "Conteudo do script interno esta vazio" }
            }
            Complete-LogProgress
            return @{ Success = $true; ExitCode = 0; Message = "Script executado com sucesso" }
        }
        
        $scriptPath = $ScriptItem.ScriptPath
        if ([string]::IsNullOrWhiteSpace($scriptPath) -or -not (Test-Path $scriptPath)) {
            return @{ Success = $false; ExitCode = -1; Message = "Arquivo do script nao encontrado" }
        }
        
        $ext = [System.IO.Path]::GetExtension($scriptPath).ToLower()
        Write-FileLog "Invoke-SystemScript: Executando arquivo '$scriptPath'" "INFO"
        Update-LogProgress "$scriptName" "[Executando arquivo...]"
        Update-Stage "Installing"
        
        switch ($ext) {
            ".ps1" {
                $process = Start-Process powershell.exe -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$scriptPath`"" -PassThru -WindowStyle Normal
            }
            ".cmd" {
                $process = Start-Process cmd.exe -ArgumentList "/c", "`"$scriptPath`"" -PassThru -WindowStyle Normal
            }
            ".bat" {
                $process = Start-Process cmd.exe -ArgumentList "/c", "`"$scriptPath`"" -PassThru -WindowStyle Normal
            }
            ".exe" {
                $process = Start-Process $scriptPath -PassThru -WindowStyle Normal
            }
            default {
                Complete-LogProgress
                return @{ Success = $false; ExitCode = -1; Message = "Extensao nao suportada: $ext" }
            }
        }
        
        $script:CurrentProcess = $process
        while (-not $process.HasExited) {
            [System.Windows.Forms.Application]::DoEvents()
            
            if ($script:CancelRequested) {
                try { $process.Kill() } catch {}
                Complete-LogProgress
                return @{ Success = $false; ExitCode = -1; Message = "Cancelado pelo usuario" }
            }
            
            Start-Sleep -Milliseconds 50
        }
        
        Complete-LogProgress
        if ($process.ExitCode -eq 0) {
            return @{ Success = $true; ExitCode = 0; Message = "Script executado com sucesso" }
        }
        
        return @{ Success = $false; ExitCode = $process.ExitCode; Message = "Codigo de saida: $($process.ExitCode)" }
    } catch {
        Complete-LogProgress
        $errorMessage = if ([string]::IsNullOrWhiteSpace($_.Exception.Message)) { $_.ToString() } else { $_.Exception.Message }
        if ([string]::IsNullOrWhiteSpace($errorMessage)) { $errorMessage = "Erro desconhecido ao executar script" }
        Write-FileLog "Invoke-SystemScript: Falha em '$scriptName' - $errorMessage" "ERROR"
        return @{ Success = $false; ExitCode = -1; Message = $errorMessage }
    } finally {
        $script:CurrentProcess = $null
    }
}

# ============================================
# FUNCAO: Obter versao mais recente via Winget Show
# ============================================
function Get-WingetAppLatestVersion {
    param([string]$AppId)
    
    try {
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = Get-BananaSuisaWingetExe
        $psi.Arguments = "show --id $AppId --exact -s winget --accept-source-agreements --disable-interactivity"
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true
        $psi.UseShellExecute = $false
        $psi.CreateNoWindow = $true
        $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
        
        $p = [System.Diagnostics.Process]::Start($psi)
        
        # Ler output sem bloquear a UI
        $outputBuilder = New-Object System.Text.StringBuilder
        while (-not $p.HasExited) {
            while (-not $p.StandardOutput.EndOfStream) {
                $line = $p.StandardOutput.ReadLine()
                if ($line) { [void]$outputBuilder.AppendLine($line) }
            }
            [System.Windows.Forms.Application]::DoEvents()
            Start-Sleep -Milliseconds 50
        }
        # Ler qualquer output restante
        $remaining = $p.StandardOutput.ReadToEnd()
        if ($remaining) { [void]$outputBuilder.Append($remaining) }
        $output = $outputBuilder.ToString()
        
        # Tentar localizar versao em saidas localizadas do winget
        if ($output -match '(?im)^\s*Vers[^:\r\n]*o\s*:\s*(.+)$') {
            return $matches[1].Trim()
        }
        if ($output -match '(?im)^\s*Version\s*:\s*(.+)$') {
            return $matches[1].Trim()
        }
    } catch {
        Write-Log "Erro ao buscar versao de $AppId : $_" -Type "Warning"
    }
    return $null
}

# ============================================
# FUNCAO: Gerenciar Cache de Instaladores
# ============================================
function Get-LocalInstaller {
    param([string]$AppId, [string]$Version)
    
    if (-not $script:UseWorkspace -or -not $Version) { return $null }
    
    $cacheDir = $script:AppPaths.Installers
    if ([string]::IsNullOrWhiteSpace($cacheDir) -or -not (Test-Path $cacheDir)) { return $null }
    $pattern = "${AppId}_${Version}.*"
    $file = Get-ChildItem -Path $cacheDir -Filter $pattern | Select-Object -First 1
    
    if ($file) { return $file.FullName }
    return $null
}

function Get-LatestLocalInstallerRecord {
    param([string]$AppId)
    
    $cacheDir = $script:AppPaths.Installers
    if ([string]::IsNullOrWhiteSpace($AppId) -or [string]::IsNullOrWhiteSpace($cacheDir) -or -not (Test-Path $cacheDir)) {
        return $null
    }
    
    $file = Get-ChildItem -Path $cacheDir -Filter "${AppId}_*" -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Extension -match '\.(exe|msi|msix|msixbundle|appx|appxbundle|zip)$' } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    
    if (-not $file) { return $null }
    
    $prefix = "${AppId}_"
    $version = ""
    if ($file.BaseName.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        $version = $file.BaseName.Substring($prefix.Length)
    }
    
    return @{
        Path = $file.FullName
        Version = $version
        Extension = $file.Extension
        LastWriteTime = $file.LastWriteTime
    }
}

function Get-WingetInstallerUrl {
    param([string]$AppId)
    
    if ([string]::IsNullOrWhiteSpace($AppId)) { return $null }
    
    try {
        $showOutput = & (Get-BananaSuisaWingetExe) show --id $AppId --exact --source winget --accept-source-agreements --disable-interactivity 2>$null | Out-String
        if ([string]::IsNullOrWhiteSpace($showOutput)) { return $null }
        
        $patterns = @(
            '(?im)^\s*URL do instalador\s*:\s*(https?://\S+)',
            '(?im)^\s*Installer Url\s*:\s*(https?://\S+)',
            '(?im)^\s*Installer URL\s*:\s*(https?://\S+)'
        )
        
        foreach ($pattern in $patterns) {
            if ($showOutput -match $pattern) {
                return $matches[1].Trim()
            }
        }
    } catch {
        Write-FileLog "Get-WingetInstallerUrl: Falha ao consultar winget show para $AppId - $($_.Exception.Message)" "WARN"
    }
    
    return $null
}

function Download-ToInstallers {
    param([string]$AppId, [string]$Version, [string]$AppName)
    
    if (-not $script:UseWorkspace -or -not $Version) { return $null }
    
    $cacheDir = $script:AppPaths.Installers
    $tempRoot = $script:AppPaths.Temp
    if ([string]::IsNullOrWhiteSpace($cacheDir) -or [string]::IsNullOrWhiteSpace($tempRoot)) {
    Write-FileLog "Download-ToInstallers: Pasta local/Temp nao configurada para $AppId" "ERROR"
    return $null
}

if (-not (Test-Path $cacheDir)) {
    New-Item -ItemType Directory -Path $cacheDir -Force | Out-Null
}
if (-not (Test-Path $tempRoot)) {
    New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
}

$safeAppId = ($AppId -replace '[^\w\.-]', '_')
$tempDir = Join-Path $tempRoot "Download_$safeAppId"
if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue }
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

Write-Log "Baixando $AppName v$Version para pasta local..." -Type "Progress"
Update-LogProgress "$AppName" "[Baixando...]"
    Update-Stage "Downloading"
    
    Write-FileLog "Download-ToInstallers: Iniciando download de $AppId v$Version" "INFO"
    
    $downloadSuccess = $false
    
    # Mapeamento de URLs Standalone Prioritarias
    $standaloneUrls = @{
        "Google.Chrome"   = "https://dl.google.com/tag/s/appguid%3D%7B8A69D345-D564-463C-AFF1-A69D9E530F96%7D%26iid%3D%7B690820B7-43A0-08BF-5C3A-0A6A71E19028%7D%26lang%3Den%26browser%3D4%26usagestats%3D0%26appname%3DGoogle%2520Chrome%26needsadmin%3Dprefers%26ap%3Dx64-stable%26installdataindex%3Ddefaultbrowser/dl/chrome/install/googlechromestandaloneenterprise64.msi"
        "Mozilla.Firefox" = "https://download.mozilla.org/?product=firefox-latest-ssl&os=win64&lang=pt-BR"
        "Brave.Brave"     = "https://referrals.brave.com/latest/BraveBrowserSetup.exe" # Fallback generico
        "Opera.Opera"     = "https://net.geo.opera.com/opera/stable/windows"
    }
    
    if ($standaloneUrls.ContainsKey($AppId)) {
        Write-FileLog "Download-ToInstallers: AppId $AppId encontrado no mapa standalone. Tentando download direto." "INFO"
        Write-Log "Tentando download direto (standalone) para $AppName..." -Type "Info"
        Update-LogProgress "$AppName" "[Baixando standalone...]"
        
        try {
            $installerUrl = $standaloneUrls[$AppId]
            $uri = [System.Uri]$installerUrl
            $fileName = [System.IO.Path]::GetFileName($uri.LocalPath)
            
            # Ajustar extensao se necessario
            if ([string]::IsNullOrWhiteSpace($fileName) -or $fileName -notmatch "\.(exe|msi|msix|msixbundle|appx|appxbundle|zip)$") {
                if ($installerUrl -match "\.msi" -or $AppId -eq "Google.Chrome") { $fileName = "$AppId.msi" } else { $fileName = "$AppId.exe" }
            }
            
            $webDestPath = Join-Path $tempDir $fileName
            Write-FileLog "Download-ToInstallers: Baixando via WebRequest de $installerUrl" "INFO"
            
            $ProgressPreference = 'SilentlyContinue'
            Invoke-WebRequest -Uri $installerUrl -OutFile $webDestPath -UseBasicParsing -TimeoutSec 600
            $ProgressPreference = 'Continue'
            
            if ((Test-Path $webDestPath) -and (Get-Item $webDestPath).Length -gt 1024) {
                $downloadSuccess = $true
                Write-FileLog "Download-ToInstallers: Download standalone finalizado com sucesso" "INFO"
            } else {
                Write-FileLog "Download-ToInstallers: Arquivo baixado via standalone invalido ou vazio" "WARN"
            }
        } catch {
            Write-FileLog "Download-ToInstallers: Falha no download standalone - $_" "ERROR"
            Write-Log "Download direto falhou. Tentando via WinGet..." -Type "Warning"
        }
    }
    
    # Se standalone funcionou, pular resto. Senao, segue normal.
    if (-not $downloadSuccess) {
        # GitHub#4648/#4695: Forcar WinINet para evitar falhas do Delivery Optimization
        $wingetSettingsPath = $null
    $originalSettings = $null
    $settingsModified = $false
    try {
        $packagedPath = Join-Path $env:LOCALAPPDATA "Packages\Microsoft.DesktopAppInstaller_8wekyb3d8bbwe\LocalState\settings.json"
        $nonPackagedPath = Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Settings\settings.json"
        if (Test-Path $packagedPath) { $wingetSettingsPath = $packagedPath }
        elseif (Test-Path $nonPackagedPath) { $wingetSettingsPath = $nonPackagedPath }
        
        if ($wingetSettingsPath) {
            $originalSettings = Get-Content $wingetSettingsPath -Raw -ErrorAction SilentlyContinue
            $settingsObj = $null
            if ($originalSettings) {
                try { $settingsObj = $originalSettings | ConvertFrom-Json -ErrorAction Stop } catch { $settingsObj = $null }
            }
            if (-not $settingsObj) { $settingsObj = [PSCustomObject]@{} }
            
            $needsChange = $true
            if ($settingsObj.PSObject.Properties['network'] -and $settingsObj.network.PSObject.Properties['downloader']) {
                if ($settingsObj.network.downloader -eq 'wininet') { $needsChange = $false }
            }
            
            if ($needsChange) {
                if (-not $settingsObj.PSObject.Properties['network']) {
                    $settingsObj | Add-Member -NotePropertyName 'network' -NotePropertyValue ([PSCustomObject]@{ downloader = 'wininet' })
                } else {
                    if ($settingsObj.network.PSObject.Properties['downloader']) {
                        $settingsObj.network.downloader = 'wininet'
                    } else {
                        $settingsObj.network | Add-Member -NotePropertyName 'downloader' -NotePropertyValue 'wininet'
                    }
                }
                $settingsObj | ConvertTo-Json -Depth 10 | Set-Content $wingetSettingsPath -Encoding UTF8 -Force
                $settingsModified = $true
                Write-FileLog "Download-ToInstallers: WinGet downloader alterado para WinINet (bypass DO)" "INFO"
            }
        }
    } catch {
        Write-FileLog "Download-ToInstallers: Falha ao configurar WinINet downloader - $_" "WARN"
    }
    
    $downloadSuccess = $false
    $hashOverrideEnabled = $false
    
    # Fase 1: Tentativas via WinGet (com WinINet ativo)
    $attempts = @(
        @{ Args = "download --id $AppId -d `"$tempDir`" --accept-source-agreements --accept-package-agreements"; Label = "winget download" },
        @{ Args = "download --id $AppId -d `"$tempDir`" --accept-source-agreements --accept-package-agreements --ignore-security-hash"; Label = "winget download (hash override)" },
        @{ Args = "install --id $AppId --download-only -l `"$tempDir`" --accept-source-agreements --accept-package-agreements --silent"; Label = "winget install --download-only" }
    )
    
    foreach ($attempt in $attempts) {
        if ($script:CancelRequested) { return $null }
        if ($downloadSuccess) { break }
        
        if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue }
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
        
        if ($attempt.Label -match "hash override" -and -not $hashOverrideEnabled) {
            try {
                $settingsProc = Start-Process -FilePath (Get-BananaSuisaWingetExe) -ArgumentList "settings --enable InstallerHashOverride" -PassThru -Wait -NoNewWindow -ErrorAction SilentlyContinue
                $hashOverrideEnabled = $true
                Write-FileLog "Download-ToInstallers: InstallerHashOverride habilitado" "INFO"
            } catch {
                Write-FileLog "Download-ToInstallers: Nao foi possivel habilitar InstallerHashOverride" "WARN"
                continue
            }
        }
        
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = Get-BananaSuisaWingetExe
        $psi.Arguments = $attempt.Args
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true
        $psi.UseShellExecute = $false
        $psi.CreateNoWindow = $true
        $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
        $psi.StandardErrorEncoding = [System.Text.Encoding]::UTF8
        
        Write-FileLog "Download-ToInstallers: Tentativa via $($attempt.Label)" "INFO"
        Update-LogProgress "$AppName" "[Baixando via $($attempt.Label)...]"
        [System.Windows.Forms.Application]::DoEvents()
        
        try {
            $p = [System.Diagnostics.Process]::Start($psi)
            $script:CurrentProcess = $p
            $errBuilder = New-Object System.Text.StringBuilder
            
            while (-not $p.HasExited) {
                if ($script:CancelRequested) {
                    try { $p.Kill() } catch {}
                    Write-FileLog "Download-ToInstallers: Cancelado pelo usuario ($AppId) durante $($attempt.Label)" "WARN"
                    return $null
                }
                while (-not $p.StandardOutput.EndOfStream) {
                    $line = $p.StandardOutput.ReadLine()
                    if ($line) {
                        if ($line -match '(\d+[\.,]?\d*)\s*(MB|KB|GB)\s*/\s*(\d+[\.,]?\d*)\s*(MB|KB|GB)') {
                            Update-LogProgress "$AppName" "[$($Matches[1]) $($Matches[2]) / $($Matches[3]) $($Matches[4])]"
                        } elseif ($line -match '(\d+)%') {
                            Update-LogProgress "$AppName" "[$($Matches[1])%]"
                        }
                    }
                }
                while (-not $p.StandardError.EndOfStream) {
                    $errLine = $p.StandardError.ReadLine()
                    if ($errLine) { [void]$errBuilder.AppendLine($errLine) }
                }
                [System.Windows.Forms.Application]::DoEvents()
                Start-Sleep -Milliseconds 100
            }
            
            $p.StandardOutput.ReadToEnd() | Out-Null
            $remainErr = $p.StandardError.ReadToEnd()
            if ($remainErr) { [void]$errBuilder.Append($remainErr) }
            $exitCode = $p.ExitCode
            $script:CurrentProcess = $null
            
            Write-FileLog "Download-ToInstallers: $($attempt.Label) exit code $exitCode para $AppId" "INFO"
            if ($errBuilder.Length -gt 0) {
                Write-FileLog "Download-ToInstallers stderr ($($attempt.Label)): $($errBuilder.ToString().Trim())" "WARN"
            }
            
            if ($exitCode -eq 0) {
                $downloadSuccess = $true
            } else {
                Write-Log "$($attempt.Label) falhou para $AppName (codigo $exitCode)" -Type "Warning"
            }
        } catch {
            Write-Log "Erro em $($attempt.Label): $_" -Type "Error"
            Write-FileLog "Download-ToInstallers: Excecao em $($attempt.Label) - $($_.Exception.Message)" "ERROR"
            $script:CurrentProcess = $null
        }
    }
    
    # Fase 2: GitHub#714 - Tentativa non-admin com --force (bypassa restricao de hash em contexto admin)
    if (-not $downloadSuccess) {
        Write-FileLog "Download-ToInstallers: Tentando via runas /trustlevel non-admin com --force" "INFO"
        Update-LogProgress "$AppName" "[Baixando via winget --force (non-admin)...]"
        [System.Windows.Forms.Application]::DoEvents()
        
        try {
            if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue }
            New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
            
            $cmdArgs = "download --id $AppId -d `"$tempDir`" --accept-source-agreements --accept-package-agreements --force"
            $wgExeForCmd = Get-BananaSuisaWingetExe
            $psi = New-Object System.Diagnostics.ProcessStartInfo
            $psi.FileName = "cmd.exe"
            $psi.Arguments = "/c runas /trustlevel:0x20000 `"`"$wgExeForCmd`" $cmdArgs`""
            $psi.RedirectStandardOutput = $true
            $psi.RedirectStandardError = $true
            $psi.UseShellExecute = $false
            $psi.CreateNoWindow = $true
            $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
            $psi.StandardErrorEncoding = [System.Text.Encoding]::UTF8
            
            $p = [System.Diagnostics.Process]::Start($psi)
            $script:CurrentProcess = $p
            $p.WaitForExit(600000)
            if (-not $p.HasExited) { try { $p.Kill() } catch {} }
            $exitCode = $p.ExitCode
            $script:CurrentProcess = $null
            
            Write-FileLog "Download-ToInstallers: runas non-admin --force exit code $exitCode para $AppId" "INFO"
            
            $foundFile = Get-ChildItem -Path $tempDir -Recurse -File -ErrorAction SilentlyContinue |
                Where-Object { $_.Extension -match '\.(exe|msi|msix|msixbundle|appx|appxbundle|zip)$' } |
                Select-Object -First 1
            if ($foundFile -and $foundFile.Length -gt 1024) {
                $downloadSuccess = $true
                Write-FileLog "Download-ToInstallers: runas non-admin --force obteve arquivo" "INFO"
            } else {
                Write-Log "winget --force (non-admin) falhou para $AppName" -Type "Warning"
            }
        } catch {
            Write-FileLog "Download-ToInstallers: Excecao em runas non-admin --force - $_" "ERROR"
            $script:CurrentProcess = $null
        }
    }
    
    # Fase 3: Fallback HTTP direto (Invoke-WebRequest) extraindo URL do manifesto
    if (-not $downloadSuccess) {
        Write-FileLog "Download-ToInstallers: Todas tentativas WinGet falharam. Iniciando fallback HTTP para $AppId" "WARNING"
        Write-Log "WinGet falhou. Baixando direto via HTTP..." -Type "Warning"
        Update-LogProgress "$AppName" "[Baixando via HTTP direto...]"
        [System.Windows.Forms.Application]::DoEvents()
        
        try {
            if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue }
            New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
            
            $installerUrl = Get-WingetInstallerUrl -AppId $AppId
            
            if ($installerUrl) {
                $uri = [System.Uri]$installerUrl
                $fileName = [System.IO.Path]::GetFileName($uri.LocalPath)
                if ([string]::IsNullOrWhiteSpace($fileName) -or $fileName -notmatch "\.(exe|msi|msix|msixbundle|appx|appxbundle|zip)$") {
                    if ($installerUrl -match "\.msi") { $fileName = "$AppId.msi" } else { $fileName = "$AppId.exe" }
                }
                $webDestPath = Join-Path $tempDir $fileName
                Write-FileLog "Download-ToInstallers: HTTP fallback de $installerUrl" "INFO"
                
                $ProgressPreference = 'SilentlyContinue'
                Invoke-WebRequest -Uri $installerUrl -OutFile $webDestPath -UseBasicParsing -TimeoutSec 600
                $ProgressPreference = 'Continue'
                
                if ((Test-Path $webDestPath) -and (Get-Item $webDestPath).Length -gt 1024) {
                    $downloadSuccess = $true
                    Write-FileLog "Download-ToInstallers: HTTP fallback finalizado com sucesso" "INFO"
                }
            } else {
                Write-FileLog "Download-ToInstallers: Nao foi possivel extrair URL do instalador via winget show" "WARN"
            }
        } catch {
            Write-FileLog "Download-ToInstallers: Falha no HTTP fallback - $_" "ERROR"
        }
    }
    
    # Restaurar settings originais do WinGet
    if ($settingsModified -and $wingetSettingsPath) {
        try {
            if ($originalSettings) {
                Set-Content $wingetSettingsPath -Value $originalSettings -Encoding UTF8 -Force
            } else {
                Remove-Item $wingetSettingsPath -Force -ErrorAction SilentlyContinue
            }
            Write-FileLog "Download-ToInstallers: Settings do WinGet restauradas ao original" "INFO"
        } catch {
            Write-FileLog "Download-ToInstallers: Falha ao restaurar settings do WinGet - $_" "WARN"
        }
    }
    }
    
    # Localizar arquivo baixado (winget pode criar subpastas)
    $downloadedFile = $null
    if (Test-Path $tempDir) {
        $downloadedFile = Get-ChildItem -Path $tempDir -Recurse -File -ErrorAction SilentlyContinue | 
            Where-Object { $_.Extension -match '\.(exe|msi|msix|msixbundle|appx|appxbundle|zip)$' } |
            Sort-Object Length -Descending |
            Select-Object -First 1
    }
    
    if ($downloadedFile) {
        # Remover versoes antigas do mesmo AppId localmente
        Get-ChildItem -Path $cacheDir -Filter "${AppId}_*" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
        
        $extension = $downloadedFile.Extension
        $newName = "${AppId}_${Version}${extension}"
        $destPath = Join-Path $cacheDir $newName
        
        Move-Item -Path $downloadedFile.FullName -Destination $destPath -Force
        Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
        
        $sizeMB = [Math]::Round($downloadedFile.Length / 1MB, 1)
        Write-Log "Instalador de $AppName v$Version salvo localmente ($($sizeMB)MB)." -Type "Success"
        return $destPath
    }
    
    Write-Log "Nenhum instalador encontrado para $AppName apos download." -Type "Warning"
    Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    return $null
}

# ============================================
# FUNCAO INSTALL-APP
# ============================================
function Install-AppWithWinget {
    param(
        [string]$AppId,
        [string]$AppName,
        [bool]$OfflineOnly = $false
    )
    
    if ($script:CancelRequested) {
        return @{ Success = $false; ExitCode = -1; Message = "Cancelado" }
    }
    
    # Tentar fechar processos relacionados para evitar erro "sistema em uso"
    Stop-AppProcesses -AppId $AppId -AppName $AppName
    
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $installerPath = $null
    
    if ($OfflineOnly) {
        Update-LogProgress "$AppName" "[Verificando instalador local...]"
        Update-Stage "Searching"
        
        $cachedRecord = Get-LatestLocalInstallerRecord -AppId $AppId
        if ($cachedRecord -and (Test-Path $cachedRecord.Path)) {
            $installerPath = $cachedRecord.Path
            Write-Log "$($AppName): Usando instalador offline: $(Split-Path $installerPath -Leaf)" -Type "Info"
        } else {
            Complete-LogProgress
            Update-Stage "Failed"
            return @{ Success = $false; ExitCode = -1; Message = "Instalador local nao encontrado" }
        }
    } else {
        # MODO ONLINE (Padrao): Direto via WinGet para maior velocidade e compatibilidade
        Update-LogProgress "$AppName" "[Instalando via WinGet (Online)...]"
        Update-Stage "Installing"
        
        $psi.FileName = Get-BananaSuisaWingetExe
        $psi.Arguments = "install -e --id $AppId --accept-source-agreements --accept-package-agreements --silent"
        Write-Log "$($AppName): Iniciando instalacao via WinGet (Online)..." -Type "Info"
    }

    # Se temos um instalador local (Modo Offline), configurar PSI adequadamente
    if ($installerPath) {
        if (-not (Test-Path $installerPath)) {
            Complete-LogProgress
            Update-Stage "Failed"
            return @{ Success = $false; ExitCode = -1; Message = "Instalador nao encontrado no disco: $installerPath" }
        }

        $ext = [System.IO.Path]::GetExtension($installerPath).ToLower()
        
        if ($ext -eq '.zip') {
            Write-Log "$($AppName): Extraindo ZIP do local..." -Type "Info"
            $extractDir = Join-Path $env:TEMP "BananaSuisa_Install_$($AppId -replace '[^\w\.-]', '_')"
            if (Test-Path $extractDir) { Remove-Item $extractDir -Recurse -Force -ErrorAction SilentlyContinue }
            try {
                Expand-Archive -Path $installerPath -DestinationPath $extractDir -Force -ErrorAction Stop
            } catch {
                Complete-LogProgress
                Update-Stage "Failed"
                return @{ Success = $false; ExitCode = -1; Message = "Falha ao extrair ZIP: $($_.Exception.Message)" }
            }
            $inner = Get-ChildItem -Path $extractDir -Recurse -File -ErrorAction SilentlyContinue |
                Where-Object { $_.Extension -match '\.(exe|msi)$' } |
                Select-Object -First 1
            if (-not $inner) {
                Complete-LogProgress
                Update-Stage "Failed"
                return @{ Success = $false; ExitCode = -1; Message = "Nenhum executavel encontrado dentro do ZIP" }
            }
            $installerPath = $inner.FullName
            $ext = $inner.Extension.ToLower()
        }
        
        if ($ext -match '\.msixbundle|\.msix|\.appxbundle|\.appx') {
            $psi.FileName = "powershell.exe"
            $psi.Arguments = "-NoProfile -Command `"Add-AppxPackage -Path '$installerPath' -ForceApplicationShutdown`""
        } elseif ($ext -eq '.msi') {
            $psi.FileName = "msiexec.exe"
            $psi.Arguments = "/i `"$installerPath`" /qn /norestart"
        } else {
            # Tentar extrair argumentos silenciosos se estivermos online (heuristicas mantidas para maior chance de sucesso)
            $silentArgs = "/silent /quiet /S /norestart"
            try {
                $showInfo = & (Get-BananaSuisaWingetExe) show --id $AppId --exact -s winget --accept-source-agreements --disable-interactivity 2>&1 | Out-String
                if ($showInfo -match 'Instalador silencioso:\s*([^\r\n]+)') { $silentArgs = $matches[1].Trim() }
                elseif ($showInfo -match 'Silent:\s*([^\r\n]+)') { $silentArgs = $matches[1].Trim() }
                elseif ($showInfo -match 'Inno') { $silentArgs = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-" }
                elseif ($showInfo -match 'Nullsoft') { $silentArgs = "/S" }
                elseif ($showInfo -match 'Burn|Wix') { $silentArgs = "/quiet /norestart" }
            } catch {}
            
            $psi.FileName = $installerPath
            $psi.Arguments = $silentArgs
        }
        Write-Log "$($AppName): Executando instalador local ($ext)..." -Type "Info"
    }
    
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
    $psi.StandardErrorEncoding = [System.Text.Encoding]::UTF8
    
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi
    $script:CurrentProcess = $process
    
    $currentStage = "Instalando..."
    $downloadProgress = ""
    
    try {
        $process.Start() | Out-Null
        
        while (-not $process.HasExited) {
            # Manter a UI responsiva em cada ciclo
            [System.Windows.Forms.Application]::DoEvents()

            if ($script:CancelRequested) {
                $process.Kill()
                Complete-LogProgress
                return @{ Success = $false; ExitCode = -1; Message = "Cancelado pelo usuario" }
            }
            
            # Leitura nao-bloqueante: so entra se houver dados no buffer
            while ($process.StandardOutput.Peek() -ne -1) {
                $line = $process.StandardOutput.ReadLine()
                if ($line) {
                    # Detectar estagio
                    if ($line -match "Found") { 
                        $currentStage = "Encontrado"
                        Update-Stage "Searching"
                        Update-LogProgress "$AppName" "[$currentStage]"
                    }
                    elseif ($line -match "Downloading") { 
                        $currentStage = "Baixando..."
                        Update-Stage "Downloading"
                    }
                    elseif ($line -match "Installing") { 
                        $currentStage = "Instalando..."
                        Update-Stage "Installing"
                        Update-LogProgress "$AppName" "[$currentStage]"
                    }
                    elseif ($line -match "Successfully") { 
                        $currentStage = "Concluido!"
                        Update-Stage "Completed"
                    }
                    
                    # Detectar progresso de download (ex: "  2.5 MB / 15.3 MB" ou percentual)
                    if ($line -match '(\d+[\.,]?\d*)\s*(KB|MB|GB)\s*/\s*(\d+[\.,]?\d*)\s*(KB|MB|GB)') {
                        $downloaded = "$($matches[1])$($matches[2])"
                        $total = "$($matches[3])$($matches[4])"
                        $downloadProgress = "[$downloaded/$total]"
                        Update-LogProgress "$AppName - Baixando..." $downloadProgress
                    }
                    elseif ($line -match '(\d+)%') {
                        $percent = $matches[1]
                        Update-LogProgress "$AppName - $currentStage" "[$percent%]"
                    }
                }
                # Processar eventos entre linhas se houver muitas
                [System.Windows.Forms.Application]::DoEvents()
            }
            while ($process.StandardError.Peek() -ne -1) {
                $errLine = $process.StandardError.ReadLine()
                if ($errLine) { Write-Log "$AppName (WinGet): $errLine" -Type "Warning" }
                [System.Windows.Forms.Application]::DoEvents()
            }
            
            Start-Sleep -Milliseconds 50
        }
        
        Complete-LogProgress
        
        $exitCode = $process.ExitCode
        $errorInfo = $script:WingetErrors[$exitCode]
        
        if ($errorInfo) {
            $message = $errorInfo.Message
            $logType = $errorInfo.Type
        } else {
            $message = if ($exitCode -eq 0) { "Instalado com sucesso" } else { "Codigo: $exitCode" }
            $logType = if ($exitCode -eq 0) { "Success" } else { "Error" }
        }
        
        if ($exitCode -eq 0) { Update-Stage "Completed" } else { Update-Stage "Failed" }
        
        return @{ 
            Success = ($exitCode -eq 0 -or $exitCode -eq 3010 -or $exitCode -eq -1978335134 -or $exitCode -eq -1978334963 -or $exitCode -eq -1978334962)
            ExitCode = $exitCode
            Message = $message
            RebootRequired = ($exitCode -eq 3010)
        }
    } catch {
        Complete-LogProgress
        Update-Stage "Failed"
        return @{ Success = $false; ExitCode = -1; Message = $_.Exception.Message }
    } finally {
        $script:CurrentProcess = $null
    }
}

# ============================================
# FUNCAO UPDATE-APP
# ============================================
function Update-AppWithWinget {
    param([string]$AppId, [string]$AppName)
    
    if ($script:CancelRequested) {
        return @{ Success = $false; ExitCode = -1; Message = "Cancelado" }
    }
    
    # Encerrar processos do app antes de atualizar (forca bruta para TI)
    Update-LogProgress "$AppName" "[Encerrando processos...]"
    $killed = Stop-AppProcesses -AppId $AppId -AppName $AppName
    if ($killed -gt 0) {
        Start-Sleep -Milliseconds 500  # Aguardar processos encerrarem
    }
    
    Update-LogProgress "$AppName" "[Buscando...]"
    Update-Stage "Searching"
    
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = Get-BananaSuisaWingetExe
    $psi.Arguments = "upgrade --id $AppId --accept-source-agreements --accept-package-agreements --silent"
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
    
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi
    $script:CurrentProcess = $process
    
    $currentStage = "Buscando..."
    
    try {
        $process.Start() | Out-Null
        
        while (-not $process.HasExited) {
            # Manter a UI responsiva em cada ciclo
            [System.Windows.Forms.Application]::DoEvents()

            if ($script:CancelRequested) {
                $process.Kill()
                Complete-LogProgress
                return @{ Success = $false; ExitCode = -1; Message = "Cancelado" }
            }
            
            # Leitura nao-bloqueante: so entra se houver dados no buffer
            while ($process.StandardOutput.Peek() -ne -1) {
                $line = $process.StandardOutput.ReadLine()
                if ($line) {
                    if ($line -match "Found") { 
                        $currentStage = "Encontrado"
                        Update-LogProgress "$AppName" "[$currentStage]"
                    }
                    elseif ($line -match "Downloading") { 
                        $currentStage = "Baixando..."
                        Update-Stage "Downloading"
                    }
                    elseif ($line -match "Installing") { 
                        $currentStage = "Instalando..."
                        Update-Stage "Installing"
                        Update-LogProgress "$AppName" "[$currentStage]"
                    }
                    elseif ($line -match "Successfully") { 
                        $currentStage = "Concluido!"
                        Update-Stage "Completed"
                    }
                    
                    # Detectar progresso de download
                    if ($line -match '(\d+[\.,]?\d*)\s*(KB|MB|GB)\s*/\s*(\d+[\.,]?\d*)\s*(KB|MB|GB)') {
                        $downloaded = "$($matches[1])$($matches[2])"
                        $total = "$($matches[3])$($matches[4])"
                        Update-LogProgress "$AppName - Baixando..." "[$downloaded/$total]"
                    }
                    elseif ($line -match '(\d+)%') {
                        $percent = $matches[1]
                        Update-LogProgress "$AppName - $currentStage" "[$percent%]"
                    }
                }
                # Processar eventos entre linhas se houver muitas
                [System.Windows.Forms.Application]::DoEvents()
            }
            while ($process.StandardError.Peek() -ne -1) {
                $errLine = $process.StandardError.ReadLine()
                if ($errLine) { Write-Log "$AppName (WinGet): $errLine" -Type "Warning" }
                [System.Windows.Forms.Application]::DoEvents()
            }
            
            Start-Sleep -Milliseconds 50
        }
        
        Complete-LogProgress
        
        $exitCode = $process.ExitCode
        $errorInfo = $script:WingetErrors[$exitCode]
        
        if ($errorInfo) {
            $message = $errorInfo.Message
            $logType = $errorInfo.Type
        } else {
            $message = if ($exitCode -eq 0) { "Atualizado com sucesso" } else { "Codigo: $exitCode" }
            $logType = if ($exitCode -eq 0) { "Success" } else { "Error" }
        }
        
        if ($exitCode -eq 0) { Update-Stage "Completed" } else { Update-Stage "Failed" }
        
        return @{ 
            Success = ($exitCode -eq 0 -or $exitCode -eq 3010 -or $exitCode -eq -1978334961)
            ExitCode = $exitCode
            Message = $message
            RebootRequired = ($exitCode -eq 3010)
        }
    } catch {
        Complete-LogProgress
        Update-Stage "Failed"
        return @{ Success = $false; ExitCode = -1; Message = $_.Exception.Message }
    } finally {
        $script:CurrentProcess = $null
    }
}

# ============================================
# FUNCAO INSTALL-WINDOWS-UPDATE
# ============================================
function Install-WindowsUpdateItem {
    param($UpdateItem)
    
    if ($script:CancelRequested) {
        return @{ Success = $false; ExitCode = -1; Message = "Cancelado" }
    }
    
    $updateName = $UpdateItem.N
    $kb = if ($UpdateItem.I) { $UpdateItem.I } else { "" }
    
    Update-LogProgress "$updateName" "[Instalando...]"
    Write-Log "Instalando atualizacao: $updateName" -Type "Progress"
    
    try {
        if (-not (Get-Module -Name PSWindowsUpdate)) {
            Import-Module PSWindowsUpdate -ErrorAction Stop
        }
        
        # Se temos o objeto Update original, usar ele
        if ($UpdateItem.Update) {
            $UpdateItem.Update | Install-WindowsUpdate -AcceptAll -IgnoreReboot -ErrorAction Stop
        } else {
            # Tentar instalar pelo KB
            if ($kb) {
                Get-WindowsUpdate -KBArticleID $kb -Install -AcceptAll -IgnoreReboot -ErrorAction Stop
            } else {
                # Instalar por titulo
                Get-WindowsUpdate -Title "*$updateName*" -Install -AcceptAll -IgnoreReboot -ErrorAction Stop
            }
        }
        
        Complete-LogProgress
        return @{ Success = $true; ExitCode = 0; Message = "Instalado com sucesso" }
    } catch {
        Complete-LogProgress
        Write-Log "Erro ao instalar atualizacao: $_" -Type "Error"
        return @{ Success = $false; ExitCode = -1; Message = "Erro: $_" }
    }
}

# ============================================
# FUNCAO UPDATE-DRIVER (usa sistema avancado com multiplas fontes)
# ============================================
function Update-DriverItem {
    param($DriverItem)
    
    # Usar a funcao avancada com multiplas tentativas e fallback
    return Update-DriverItemAdvanced -DriverItem $DriverItem
}

# ============================================
# FUNCAO REMOVE-APP
# ============================================
function Remove-AppWithWinget {
    param([string]$AppId, [string]$AppName)
    
    if ($script:CancelRequested) {
        return @{ Success = $false; ExitCode = -1; Message = "Cancelado" }
    }
    
    # Encerrar processos do app antes de remover (forca bruta para TI)
    Update-LogProgress "$AppName" "[Encerrando processos...]"
    $killed = Stop-AppProcesses -AppId $AppId -AppName $AppName
    if ($killed -gt 0) {
        Start-Sleep -Milliseconds 500  # Aguardar processos encerrarem
    }
    
    # Primeiro, verificar se ha multiplos pacotes com este ID/Nome
    $packagesToRemove = @()
    
    # Termos de busca mais abrangentes
    $searchTerms = @()
    $searchTerms += $AppId
    $searchTerms += $AppName
    
    # Adicionar partes do ID (ex: "7zip" de "7zip.7zip")
    if ($AppId -match '([a-zA-Z0-9]+)\.') { $searchTerms += $matches[1] }
    
    # Adicionar primeira palavra do nome (ex: "7-Zip" -> "7-Zip")
    if ($AppName) { $searchTerms += ($AppName -split ' ')[0] }
    
    # Remover duplicatas e filtrar vazios
    $searchTerms = $searchTerms | Where-Object { $_ } | Select-Object -Unique
    
    if ($script:WinGetModuleAvailable) {
        try {
            # Estrategia 1: Busca exata pelo ID
            $exactPkg = Get-WinGetPackage -Id $AppId -MatchOption Equals -ErrorAction SilentlyContinue
            if ($exactPkg) { $packagesToRemove += $exactPkg }
            
            # Estrategia 2: Busca pelo nome exato
            $namePkg = Get-WinGetPackage -Name $AppName -MatchOption Equals -ErrorAction SilentlyContinue
            if ($namePkg) { $packagesToRemove += $namePkg }
            
            # Estrategia 3: Se nao encontrou ou queremos garantir, busca abrangente
            # Recupera TODOS os pacotes e filtra localmente (mais confiavel que -Query do winget)
            $allPackages = Get-WinGetPackage -ErrorAction SilentlyContinue
            
            foreach ($pkg in $allPackages) {
                $pkgId = $pkg.Id
                $pkgName = $pkg.Name
                
                # Normalizacao para comparacao
                $pkgIdLower = $pkgId.ToLower()
                $pkgNameLower = $pkgName.ToLower()
                
                # Verificar correspondencia EXATA primeiro (prioridade)
                if ($pkgId -eq $AppId -or $pkgName -eq $AppName) {
                     $packagesToRemove += $pkg
                     continue
                }
                
                # Verificar termos de busca
                foreach ($term in $searchTerms) {
                    if ($term.Length -lt 3) { continue } # Ignorar termos muito curtos para seguranca
                    
                    # Verifica se o termo esta contido no ID ou Nome
                    # Usamos limites de palavra para evitar falsos positivos (ex: "Java" nao pegar "JavaScript")
                    if ($pkgIdLower -match "\b$term\b" -or $pkgNameLower -match "\b$term\b") {
                        $packagesToRemove += $pkg
                        break
                    }
                    # Fallback: contem a string (para casos como "7zip" em "7zip.7zip")
                    elseif ($pkgIdLower -like "*$term*" -or $pkgNameLower -like "*$term*") {
                        # Verificacao extra de seguranca: o nome deve ser similar
                        # Ex: Se buscamos "Brave", aceitamos "Brave Browser", mas nao "BraveSoul Game"
                        if ($pkgNameLower -like "*$($AppName.Split(' ')[0].ToLower())*") {
                            $packagesToRemove += $pkg
                            break
                        }
                        
                        # Se o ID bater forte (ex: "7zip" no ID), aceita
                        if ($pkgIdLower -match $term) {
                            $packagesToRemove += $pkg
                            break
                        }
                    }
                }
            }
        } catch { }
    }
    
    # Remover duplicatas de pacotes (pelo Id)
    $packagesToRemove = $packagesToRemove | Sort-Object Id -Unique
    
    # Se encontrou multiplos pacotes (ou apenas 1, mas queremos usar o objeto correto), remover todos
    if ($packagesToRemove.Count -ge 1) {
        if ($packagesToRemove.Count -gt 1) {
            Write-Log "$AppName - Encontrados $($packagesToRemove.Count) pacotes relacionados, removendo todos..." -Type "Info"
        }
        
        $allRemoved = $true
        $removedCount = 0
        
        foreach ($pkg in $packagesToRemove) {
            if ($script:CancelRequested) { return @{ Success = $false; ExitCode = -1; Message = "Cancelado" } }
            
            $pkgDesc = if ($pkg.Version) { "$($pkg.Name) v$($pkg.Version)" } else { $pkg.Name }
            # Se for o mesmo ID que estamos tentando remover originalmente, mensagem padrao
            if ($pkg.Id -eq $AppId) {
                Update-LogProgress "$AppName" "[Removendo...]"
            } else {
                Update-LogProgress "$AppName" "[Removendo $pkgDesc...]"
            }
            
            $result = Remove-SinglePackage -PackageId $pkg.Id -AppName $AppName
            
            if ($result.Success) {
                $removedCount++
            } else {
                # Se falhar com "Multiplos pacotes", tentar pelo nome como fallback extremo
                if ($result.ExitCode -eq -1978335210) {
                     # Tentar remover pelo nome em vez do ID
                     $psiName = New-Object System.Diagnostics.ProcessStartInfo
                     $psiName.FileName = Get-BananaSuisaWingetExe
                     $psiName.Arguments = "uninstall --name `"$($pkg.Name)`" --silent --force --purge --accept-source-agreements"
                     $psiName.CreateNoWindow = $true
                     $psiName.UseShellExecute = $false
                     try { $p = [System.Diagnostics.Process]::Start($psiName); $p.WaitForExit(60000) } catch {}
                }
                $allRemoved = $false
            }
            [System.Windows.Forms.Application]::DoEvents()
        }
        
        # Verificacao final agressiva
        $stillInstalled = Test-AppInstalled -AppId $AppId
        if (-not $stillInstalled) {
             Update-Stage "Completed"
             return @{ Success = $true; ExitCode = 0; Message = "Remocao concluida" }
        }
    }
    
    # Se nao encontrou pacotes pelo modulo ou falhou, tenta metodo legado (single)
    if ($packagesToRemove.Count -eq 0) {
        Update-LogProgress "$AppName" "[Removendo...]"
        $result = Remove-SinglePackage -PackageId $AppId -AppName $AppName
    } else {
        # Ja tentamos remover acima
        $result = @{ Success = $allRemoved; ExitCode = if($allRemoved){0}else{-1} }
    }
    
    # ==============================================================================
    # FALLBACK DINAMICO: DESINSTALADOR VIA REGISTRO (ESTILO PAINEL DE CONTROLE)
    # ==============================================================================
    if (-not $result.Success) {
        Update-LogProgress "$AppName" "[Buscando desinstalador no registro...]"
        
        # Usar novo sistema dinamico que varre o registro do Windows
        $registryResult = Invoke-RegistryUninstall -AppName $AppName -AppId $AppId
        
        if ($registryResult.Success) {
            Complete-LogProgress
            Update-Stage "Completed"
            return @{ Success = $true; ExitCode = 0; Message = $registryResult.Message }
        }
    }
    
    Complete-LogProgress
    
    if ($result.Success) {
        Update-Stage "Completed"
    } else {
        # Verificacao final
        $stillInstalled = Test-AppInstalled -AppId $AppId
        if (-not $stillInstalled) {
            Update-Stage "Completed"
            Write-Log "$AppName - Removido apesar do codigo $($result.ExitCode)" -Type "Info"
            return @{ Success = $true; ExitCode = $result.ExitCode; Message = "Removido com sucesso (verificado)" }
        }
        Update-Stage "Failed"
    }
    
    return $result
}

# Funcao auxiliar para remover um pacote individual
function Remove-SinglePackage {
    param([string]$PackageId, [string]$AppName)
    
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = Get-BananaSuisaWingetExe
    # --silent: modo silencioso
    # --force: forca desinstalacao sem confirmacao
    # --disable-interactivity: desabilita prompts interativos
    # --accept-source-agreements: aceita acordos automaticamente
    # --purge: remove dados do app (quando suportado)
    $psi.Arguments = "uninstall --id `"$PackageId`" --silent --force --disable-interactivity --accept-source-agreements --purge"
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
    $psi.StandardErrorEncoding = [System.Text.Encoding]::UTF8
    
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi
    $script:CurrentProcess = $process
    
    try {
        $process.Start() | Out-Null
        
        while (-not $process.HasExited) {
            # Manter a UI responsiva em cada ciclo
            [System.Windows.Forms.Application]::DoEvents()

            if ($script:CancelRequested) {
                $process.Kill()
                return @{ Success = $false; ExitCode = -1; Message = "Cancelado" }
            }
            
            # Leitura nao-bloqueante
            while ($process.StandardOutput.Peek() -ne -1) {
                $line = $process.StandardOutput.ReadLine()
                if ($line -and $line -match '(\d+)%') {
                    Update-LogProgress "$AppName" "[$($matches[1])%]"
                }
                [System.Windows.Forms.Application]::DoEvents()
            }
            while ($process.StandardError.Peek() -ne -1) {
                $errLine = $process.StandardError.ReadLine()
                if ($errLine) { Write-Log "$AppName (WinGet): $errLine" -Type "Warning" }
                [System.Windows.Forms.Application]::DoEvents()
            }
            
            Start-Sleep -Milliseconds 50
        }
        
        $exitCode = $process.ExitCode
        $errorInfo = $script:WingetErrors[$exitCode]
        
        # Sucesso direto
        if ($exitCode -eq 0) {
            return @{ Success = $true; ExitCode = 0; Message = "Removido com sucesso" }
        }
        
        # Outros codigos - retornar para verificacao posterior
        $message = if ($errorInfo) { $errorInfo.Message } else { "Codigo: $exitCode" }
        return @{ Success = $false; ExitCode = $exitCode; Message = $message }
        
    } catch {
        return @{ Success = $false; ExitCode = -1; Message = $_.Exception.Message }
    } finally {
        $script:CurrentProcess = $null
    }
}

# ============================================
# FUNCAO SHOW-REPORT
# ============================================
function Show-Report {
    $modeText = switch ($script:CurrentMode) {
        "Install" { "INSTALACAO" }
        "InstallOffline" { "INSTALACAO OFFLINE" }
        "Update" { "ATUALIZACAO" }
        "Remove" { "REMOCAO" }
        "Scripts" { "SCRIPTS" }
        default { "OPERACAO" }
    }
    
    $report = "=== RELATORIO DE $modeText ===`n`n"
    
    $report += "Sucesso: $($script:InstallResults.Success.Count)`n"
    foreach ($app in $script:InstallResults.Success) { $report += "  - $app`n" }
    
    if ($script:InstallResults.Skipped.Count -gt 0) {
        $report += "`nIgnorados: $($script:InstallResults.Skipped.Count)`n"
        foreach ($app in $script:InstallResults.Skipped) { $report += "  - $app`n" }
    }
    
    if ($script:InstallResults.Failed.Count -gt 0) {
        $report += "`nFalhas: $($script:InstallResults.Failed.Count)`n"
        foreach ($app in $script:InstallResults.Failed) { $report += "  - $app`n" }
    }
    
    if ($script:InstallResults.RebootRequired.Count -gt 0) {
        $report += "`n[!] Requer reinicializacao:`n"
        foreach ($app in $script:InstallResults.RebootRequired) { $report += "  - $app`n" }
    }
    
    $icon = if ($script:InstallResults.Failed.Count -eq 0) { "Information" } else { "Warning" }
    [System.Windows.Forms.MessageBox]::Show($report, "$modeText Concluida", "OK", $icon)
}

# ============================================
# FUNCAO UPDATE-LAYOUT - Calculos dinamicos
# ============================================
function Update-Layout {
    # Dimensoes base
    $formW = $Form.ClientSize.Width
    $formH = $Form.ClientSize.Height
    $headerH = $Header.Height
    $footerH = $Footer.Height
    
    # Area disponivel (entre header e footer)
    $availableH = $formH - $headerH - $footerH
    $availableW = $formW
    
    # ======================================
    # SIDEBAR: 15% da largura (min 140, max 200)
    # ======================================
    $sidebarW = [Math]::Max(140, [Math]::Min(200, [int]($availableW * 0.15)))
    $SidebarPanel.Location = New-Object System.Drawing.Point(0, $headerH)
    $SidebarPanel.Size = New-Object System.Drawing.Size($sidebarW, $availableH)
    
    # ======================================
    # CONTENT: Restante da largura (85%)
    # ======================================
    $contentW = $availableW - $sidebarW
    $ContentPanel.Location = New-Object System.Drawing.Point($sidebarW, $headerH)
    $ContentPanel.Size = New-Object System.Drawing.Size($contentW, $availableH)
    
    # ======================================
    # SEARCH BOX - Posicionado a direita no Header
    # ======================================
    $searchBoxW = 260
    $searchBtnW = 108
    $searchRightMargin = 18
    $searchGap = 8
    $searchBoxX = $formW - $searchBtnW - $searchBoxW - $searchGap - $searchRightMargin
    $SearchBox.Size = New-Object System.Drawing.Size($searchBoxW, 26)
    $SearchBox.Location = New-Object System.Drawing.Point($searchBoxX, 22)
    $BtnSearchOnline.Size = New-Object System.Drawing.Size($searchBtnW, 28)
    $BtnSearchOnline.Location = New-Object System.Drawing.Point(($searchBoxX + $searchBoxW + $searchGap), 21)
    
    # Titulo e Subtitulo da View no Header (substituindo o antigo branding)
    $ViewTitle.Location = New-Object System.Drawing.Point(18, 12)
    $ViewSubtitle.Location = New-Object System.Drawing.Point(18, 42)
    
    # ======================================
    # SIDEBAR - Botoes com largura dinamica
    # ======================================
    $btnW = $sidebarW - 20  # 10px padding cada lado
    $sidebarH = $availableH
    
    # Botoes do menu principal (mesmo tamanho e espacamento dos botoes internos)
    $SidebarSectionPrimary.Location = New-Object System.Drawing.Point(10, 10)
    $SidebarSectionContext.Location = New-Object System.Drawing.Point(10, 10)
    $SidebarSectionUtility.Location = New-Object System.Drawing.Point(10, ($sidebarH - 85))

    $BtnModeInstall.Size = New-Object System.Drawing.Size($btnW, 32)
    $BtnModeInstall.Location = New-Object System.Drawing.Point(10, 34)
    
    $BtnModeUpdate.Size = New-Object System.Drawing.Size($btnW, 32)
    $BtnModeUpdate.Location = New-Object System.Drawing.Point(10, 74)
    
    $BtnModeRemove.Size = New-Object System.Drawing.Size($btnW, 32)
    $BtnModeRemove.Location = New-Object System.Drawing.Point(10, 114)
    
    $BtnModeSystem.Size = New-Object System.Drawing.Size($btnW, 32)
    $BtnModeSystem.Location = New-Object System.Drawing.Point(10, 154)

    $BtnModePrinters.Size = New-Object System.Drawing.Size($btnW, 32)
    $BtnModePrinters.Location = New-Object System.Drawing.Point(10, 194)

    $BtnModeStorage.Size = New-Object System.Drawing.Size($btnW, 32)
    $BtnModeStorage.Location = New-Object System.Drawing.Point(10, 234)
    
    # Botoes do estado ativo
    $BtnBack.Size = New-Object System.Drawing.Size($btnW, 32)
    $BtnBack.Location = New-Object System.Drawing.Point(10, 34)
    
    # Botoes do submenu Sistema
    $BtnWinUpdates.Size = New-Object System.Drawing.Size($btnW, 32)
    $BtnWinUpdates.Location = New-Object System.Drawing.Point(10, 74)
    
    $BtnDrivers.Size = New-Object System.Drawing.Size($btnW, 32)
    $BtnDrivers.Location = New-Object System.Drawing.Point(10, 114)
    
    $BtnActivator.Size = New-Object System.Drawing.Size($btnW, 32)
    $BtnActivator.Location = New-Object System.Drawing.Point(10, 154)
    
    $BtnLocalAccount.Size = New-Object System.Drawing.Size($btnW, 32)
    $BtnLocalAccount.Location = New-Object System.Drawing.Point(10, 194)
    
    $BtnScripts.Size = New-Object System.Drawing.Size($btnW, 32)
    $BtnScripts.Location = New-Object System.Drawing.Point(10, 234)

    # Botoes do submenu Impressoras
    $BtnPrinterEpsonSC.Size = New-Object System.Drawing.Size($btnW, 32)
    $BtnPrinterEpsonSC.Location = New-Object System.Drawing.Point(10, 74)

    $BtnPrinterCanonG3160.Size = New-Object System.Drawing.Size($btnW, 32)
    $BtnPrinterCanonG3160.Location = New-Object System.Drawing.Point(10, 114)

    $BtnPrinterCanonG2060.Size = New-Object System.Drawing.Size($btnW, 32)
    $BtnPrinterCanonG2060.Location = New-Object System.Drawing.Point(10, 154)

    $BtnPrinterElginL42.Size = New-Object System.Drawing.Size($btnW, 32)
    $BtnPrinterElginL42.Location = New-Object System.Drawing.Point(10, 194)

    $BtnPrinterArgoxOS.Size = New-Object System.Drawing.Size($btnW, 32)
    $BtnPrinterArgoxOS.Location = New-Object System.Drawing.Point(10, 234)

    # Botoes do submenu Cache
    $BtnStorageWinget.Size = New-Object System.Drawing.Size($btnW, 32)
    $BtnStorageWinget.Location = New-Object System.Drawing.Point(10, 74)

    $BtnStorageApps.Size = New-Object System.Drawing.Size($btnW, 32)
    $BtnStorageApps.Location = New-Object System.Drawing.Point(10, 114)

    # Modo lista (Instalar / Atualizar / Remover): Voltar, Todos, Limpar
    $BtnAll.Size = New-Object System.Drawing.Size($btnW, 32)
    $BtnAll.Location = New-Object System.Drawing.Point(10, 74)
    
    $BtnNone.Size = New-Object System.Drawing.Size($btnW, 32)
    $BtnNone.Location = New-Object System.Drawing.Point(10, 114)
    
    # Botoes Winget no rodape da sidebar
    $BtnInstallWinget.Size = New-Object System.Drawing.Size($btnW, 28)
    $BtnInstallWinget.Location = New-Object System.Drawing.Point(10, ($sidebarH - 64))
    
    $BtnRepairWinget.Size = New-Object System.Drawing.Size($btnW, 28)
    $BtnRepairWinget.Location = New-Object System.Drawing.Point(10, ($sidebarH - 32))
    
    # ======================================
    # FOOTER - Elementos com posicao dinamica
    # ======================================
    $footerW = $Footer.ClientSize.Width
    $footerH = $Footer.ClientSize.Height
    
    # Contador: canto superior direito (container separado)
    $LblCount.Location = New-Object System.Drawing.Point(($footerW - 132), 6)
    
    # Botao Action: canto direito abaixo do contador
    $btnTop = if ($LblCount.Visible) { 26 } else { 8 }
    $btnHeight = $footerH - $btnTop - 8
    $BtnAction.Size = New-Object System.Drawing.Size(120, $btnHeight)
    $BtnAction.Location = New-Object System.Drawing.Point(($footerW - 132), $btnTop)
    
    # LogBox: ocupa todo o espaco restante
    $logW = $footerW - 168  # Espaco menos botao e margens
    $LogBox.Size = New-Object System.Drawing.Size($logW, ($footerH - 16))
    $LogBox.Location = New-Object System.Drawing.Point(10, 8)
    $StageLabel.Location = New-Object System.Drawing.Point([Math]::Max(16, $logW - 120), 10)
    
    # ======================================
    # CONTENT - Elementos internos
    # ======================================
    # Welcome label - centralizar
    if ($WelcomeLabel.Visible) {
        $WelcomeLabel.Location = New-Object System.Drawing.Point(
            [Math]::Max(20, (($contentW - $WelcomeLabel.Width) / 2)),
            [Math]::Max(60, (($availableH - $WelcomeLabel.Height) / 2))
        )
    }
    
    # Lista de apps e separadores - largura baseada no ContentPanel
    $itemWidth = $contentW - 50
    if ($itemWidth -lt 400) { $itemWidth = 400 }
    
    $ListPanel.SuspendLayout()
    
    # Redimensionar todos os controles do ListPanel (itens e separadores)
    foreach ($ctrl in $ListPanel.Controls) {
        $ctrl.Width = $itemWidth
    }
    
    $ListPanel.ResumeLayout($true)
}

# ============================================
# FUNCOES DE NAVEGACAO DA SIDEBAR
# ============================================
# ============================================
# JANELA DE SELECAO DE PERFIL
# ============================================
function Show-ProfileSelector {
    $profileForm = New-Object System.Windows.Forms.Form
    $profileForm.Text = "Selecionar Perfil"
    $profileForm.ClientSize = New-Object System.Drawing.Size(400, 350)
    $profileForm.StartPosition = "CenterParent"
    $profileForm.FormBorderStyle = "FixedDialog"
    $profileForm.MaximizeBox = $false
    $profileForm.MinimizeBox = $false
    $profileForm.BackColor = $BG
    $profileForm.Font = New-Object System.Drawing.Font("Segoe UI", 9)
    
    # Titulo
    $lblTitle = New-Object System.Windows.Forms.Label
    $lblTitle.Text = "Selecione um perfil para aplicar"
    $lblTitle.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 11)
    $lblTitle.ForeColor = $Text
    $lblTitle.Location = New-Object System.Drawing.Point(15, 15)
    $lblTitle.AutoSize = $true
    $profileForm.Controls.Add($lblTitle)
    
    # Lista de perfis
    $listProfiles = New-Object System.Windows.Forms.ListView
    $listProfiles.Location = New-Object System.Drawing.Point(15, 50)
    $listProfiles.Size = New-Object System.Drawing.Size(370, 220)
    $listProfiles.View = "Details"
    $listProfiles.FullRowSelect = $true
    $listProfiles.BackColor = $ItemBG
    $listProfiles.ForeColor = $Text
    $listProfiles.BorderStyle = "None"
    $listProfiles.Columns.Add("Perfil", 150) | Out-Null
    $listProfiles.Columns.Add("Descricao", 200) | Out-Null
    $profileForm.Controls.Add($listProfiles)
    
    # Carregar perfis
    $profiles = Get-AllProfiles
    foreach ($prof in $profiles) {
        $item = New-Object System.Windows.Forms.ListViewItem($prof.Name)
        $item.SubItems.Add($prof.Description) | Out-Null
        $item.Tag = $prof
        $listProfiles.Items.Add($item) | Out-Null
    }
    
    # Botao Aplicar
    $btnApply = New-Object System.Windows.Forms.Button
    $btnApply.Text = "Aplicar Perfil"
    $btnApply.Size = New-Object System.Drawing.Size(120, 35)
    $btnApply.Location = New-Object System.Drawing.Point(150, 285)
    $btnApply.FlatStyle = "Flat"
    $btnApply.BackColor = $Accent
    $btnApply.ForeColor = $Text
    $profileForm.Controls.Add($btnApply)
    
    # Botao Cancelar
    $btnCancel = New-Object System.Windows.Forms.Button
    $btnCancel.Text = "Cancelar"
    $btnCancel.Size = New-Object System.Drawing.Size(100, 35)
    $btnCancel.Location = New-Object System.Drawing.Point(280, 285)
    $btnCancel.FlatStyle = "Flat"
    $btnCancel.BackColor = $ItemBG
    $btnCancel.ForeColor = $TextDim
    $profileForm.Controls.Add($btnCancel)
    
    # Eventos
    $btnApply.Add_Click({
        if ($listProfiles.SelectedItems.Count -eq 0) {
            [System.Windows.Forms.MessageBox]::Show("Selecione um perfil.", "Aviso", "OK", "Warning")
            return
        }
        
        $selectedProfile = $listProfiles.SelectedItems[0].Tag
        $profileApps = $selectedProfile.Apps
        
        # Limpar todas as selecoes primeiro
        foreach ($cb in $script:Checkboxes) {
            $cb.Checked = $false
        }
        
        # Marcar apps do perfil
        $markedCount = 0
        foreach ($cb in $script:Checkboxes) {
            $appId = $cb.Tag.I
            if ($profileApps -contains $appId) {
                $cb.Checked = $true
                $markedCount++
            }
        }
        
        # Atualizar contador
        & $script:UpdateCount
        
        Write-Log "Perfil '$($selectedProfile.Name)' aplicado ($markedCount apps selecionados)" -Type "Success"
        $profileForm.Close()
    })
    
    $btnCancel.Add_Click({
        $profileForm.Close()
    })
    
    # Duplo clique para aplicar
    $listProfiles.Add_DoubleClick({
        $btnApply.PerformClick()
    })
    
    [void]$profileForm.ShowDialog()
}

# ============================================
# JANELA DE GERENCIAMENTO DE APPS
# ============================================
function Show-AppManager {
    $appForm = New-Object System.Windows.Forms.Form
    $appForm.Text = "Gerenciar Aplicativos"
    $appForm.ClientSize = New-Object System.Drawing.Size(700, 550)
    $appForm.StartPosition = "CenterParent"
    $appForm.FormBorderStyle = "FixedDialog"
    $appForm.MaximizeBox = $false
    $appForm.BackColor = $BG
    $appForm.Font = New-Object System.Drawing.Font("Segoe UI", 9)

    # Painel superior - Adicionar App
    $addPanel = New-Object System.Windows.Forms.Panel
    $addPanel.Location = New-Object System.Drawing.Point(10, 10)
    $addPanel.Size = New-Object System.Drawing.Size(680, 100)
    $addPanel.BackColor = $Panel
    $appForm.Controls.Add($addPanel)

    $lblAdd = New-Object System.Windows.Forms.Label
    $lblAdd.Text = "Adicionar Aplicativo"
    $lblAdd.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 11)
    $lblAdd.ForeColor = $Text
    $lblAdd.Location = New-Object System.Drawing.Point(10, 8)
    $lblAdd.AutoSize = $true
    $addPanel.Controls.Add($lblAdd)

    $lblPaste = New-Object System.Windows.Forms.Label
    $lblPaste.Text = "Cole o comando do winget.run ou digite o ID:"
    $lblPaste.ForeColor = $TextDim
    $lblPaste.Location = New-Object System.Drawing.Point(10, 35)
    $lblPaste.AutoSize = $true
    $addPanel.Controls.Add($lblPaste)

    $txtWingetCmd = New-Object System.Windows.Forms.TextBox
    $txtWingetCmd.Location = New-Object System.Drawing.Point(10, 58)
    $txtWingetCmd.Size = New-Object System.Drawing.Size(450, 24)
    $txtWingetCmd.BackColor = $ItemBG
    $txtWingetCmd.ForeColor = $Text
    $txtWingetCmd.BorderStyle = "FixedSingle"
    $txtWingetCmd.Font = New-Object System.Drawing.Font("Consolas", 9)
    $addPanel.Controls.Add($txtWingetCmd)

    $lblCat = New-Object System.Windows.Forms.Label
    $lblCat.Text = "Categoria:"
    $lblCat.ForeColor = $TextDim
    $lblCat.Location = New-Object System.Drawing.Point(470, 60)
    $lblCat.AutoSize = $true
    $addPanel.Controls.Add($lblCat)

    $cmbCat = New-Object System.Windows.Forms.ComboBox
    $cmbCat.Location = New-Object System.Drawing.Point(535, 57)
    $cmbCat.Size = New-Object System.Drawing.Size(80, 24)
    $cmbCat.DropDownStyle = "DropDown"
    $cmbCat.BackColor = $ItemBG
    $cmbCat.ForeColor = $Text
    @("TI", "Utilitarios", "Navegadores", "Escritorio", "Comunicacao", "Acesso Remoto", "Midia", "Dev", "Runtime", "Seguranca", "Design", "Online") | ForEach-Object { $cmbCat.Items.Add($_) }
    $cmbCat.SelectedIndex = 0
    $addPanel.Controls.Add($cmbCat)

    $btnAdd = New-Object System.Windows.Forms.Button
    $btnAdd.Text = "Adicionar"
    $btnAdd.Size = New-Object System.Drawing.Size(80, 28)
    $btnAdd.Location = New-Object System.Drawing.Point(620, 55)
    $btnAdd.FlatStyle = "Flat"
    $btnAdd.BackColor = $Green
    $btnAdd.ForeColor = $Text
    $addPanel.Controls.Add($btnAdd)

    # Lista de apps
    $lblList = New-Object System.Windows.Forms.Label
    $lblList.Text = "Aplicativos Disponiveis (selecione para remover)"
    $lblList.Font = New-Object System.Drawing.Font("Segoe UI Semibold", 11)
    $lblList.ForeColor = $Text
    $lblList.Location = New-Object System.Drawing.Point(10, 120)
    $lblList.AutoSize = $true
    $appForm.Controls.Add($lblList)

    $listApps = New-Object System.Windows.Forms.ListView
    $listApps.Location = New-Object System.Drawing.Point(10, 145)
    $listApps.Size = New-Object System.Drawing.Size(680, 350)
    $listApps.View = "Details"
    $listApps.FullRowSelect = $true
    $listApps.BackColor = $ItemBG
    $listApps.ForeColor = $Text
    $listApps.BorderStyle = "None"
    $listApps.Columns.Add("Nome", 200) | Out-Null
    $listApps.Columns.Add("ID", 280) | Out-Null
    $listApps.Columns.Add("Categoria", 100) | Out-Null
    $listApps.Columns.Add("Tipo", 80) | Out-Null
    $appForm.Controls.Add($listApps)

    $btnRemove = New-Object System.Windows.Forms.Button
    $btnRemove.Text = "Remover Selecionado"
    $btnRemove.Size = New-Object System.Drawing.Size(150, 35)
    $btnRemove.Location = New-Object System.Drawing.Point(10, 505)
    $btnRemove.FlatStyle = "Flat"
    $btnRemove.BackColor = $Red
    $btnRemove.ForeColor = $Text
    $appForm.Controls.Add($btnRemove)

    $btnClose = New-Object System.Windows.Forms.Button
    $btnClose.Text = "Fechar"
    $btnClose.Size = New-Object System.Drawing.Size(100, 35)
    $btnClose.Location = New-Object System.Drawing.Point(590, 505)
    $btnClose.FlatStyle = "Flat"
    $btnClose.BackColor = $Accent
    $btnClose.ForeColor = $Text
    $appForm.Controls.Add($btnClose)

    # Funcao para recarregar lista
    $refreshAppList = {
        $listApps.Items.Clear()
        
        # Apps do config
        if ($script:AppConfig -and $script:AppConfig.apps) {
            foreach ($app in ($script:AppConfig.apps | Sort-Object { $_.category }, { $_.name })) {
                $appId = if ($app.id) { $app.id } else { $app.I }
                $appName = if ($app.name) { $app.name } else { $app.N }
                $appCat = if ($app.category) { $app.category } else { $app.C }
                
                $item = New-Object System.Windows.Forms.ListViewItem($appName)
                $item.SubItems.Add($appId) | Out-Null
                $item.SubItems.Add($appCat) | Out-Null
                $item.SubItems.Add("Config") | Out-Null
                $item.Tag = @{ Id = $appId; Type = "Config" }
                $listApps.Items.Add($item) | Out-Null
            }
        }
        
        # CustomApps
        if ($script:AppConfig -and $script:AppConfig.customApps) {
            foreach ($app in ($script:AppConfig.customApps | Sort-Object { $_.C }, { $_.N })) {
                $appId = if ($app.I) { $app.I } else { $app.id }
                $appName = if ($app.N) { $app.N } else { $app.name }
                $appCat = if ($app.C) { $app.C } else { $app.category }
                
                $item = New-Object System.Windows.Forms.ListViewItem($appName)
                $item.SubItems.Add($appId) | Out-Null
                $item.SubItems.Add($appCat) | Out-Null
                $item.SubItems.Add("Custom") | Out-Null
                $item.ForeColor = $Yellow
                $item.Tag = @{ Id = $appId; Type = "Custom" }
                $listApps.Items.Add($item) | Out-Null
            }
        }
        
        $totalApps = $listApps.Items.Count
        $lblList.Text = "Aplicativos Disponiveis ($totalApps apps)"
    }

    # Eventos
    $btnAdd.Add_Click({
        $cmdText = $txtWingetCmd.Text
        if (-not $cmdText) {
            [System.Windows.Forms.MessageBox]::Show("Cole um comando ou digite um ID.", "Aviso", "OK", "Warning")
            return
        }

        $appId = Extract-WingetId $cmdText
        if (-not $appId) {
            [System.Windows.Forms.MessageBox]::Show("Nao foi possivel extrair o ID do aplicativo.`n`nExemplos validos:`n- winget install --id=Google.Chrome`n- Google.Chrome", "Erro", "OK", "Error")
            return
        }

        # Extrair nome do ID (ultima parte apos o ponto)
        $nameParts = $appId -split '\.'
        $appName = if ($nameParts.Count -gt 1) { $nameParts[-1] } else { $appId }
        
        # Perguntar o nome
        Add-Type -AssemblyName Microsoft.VisualBasic
        $appName = [Microsoft.VisualBasic.Interaction]::InputBox("Nome do aplicativo:", "Nome", $appName)
        if (-not $appName) { return }

        $category = if ($cmbCat.Text) { $cmbCat.Text } else { "Online" }

        # Adicionar ao config
        if (Add-AppToConfig -AppName $appName -AppId $appId -Category $category) {
            $txtWingetCmd.Text = ""
            & $refreshAppList
            [System.Windows.Forms.MessageBox]::Show("Aplicativo '$appName' adicionado!", "Sucesso", "OK", "Information")
        } else {
            [System.Windows.Forms.MessageBox]::Show("Aplicativo '$appId' ja existe.", "Aviso", "OK", "Warning")
        }
    })

    $btnRemove.Add_Click({
        if ($listApps.SelectedItems.Count -eq 0) {
            [System.Windows.Forms.MessageBox]::Show("Selecione um aplicativo para remover.", "Aviso", "OK", "Warning")
            return
        }

        $selected = $listApps.SelectedItems[0]
        $appId = $selected.Tag.Id
        $appType = $selected.Tag.Type
        $appName = $selected.Text

        $r = [System.Windows.Forms.MessageBox]::Show("Remover '$appName' ($appId) da lista?`n`nIsso tambem removera o app de todos os perfis.", "Confirmar", "YesNo", "Warning")
        if ($r -eq "Yes") {
            if ($appType -eq "Custom") {
                Remove-CustomApp -AppId $appId
            } else {
                Remove-AppFromConfig -AppId $appId
            }
            & $refreshAppList
        }
    })

    $btnClose.Add_Click({
        $appForm.Close()
    })

    # Carregar lista inicial
    & $refreshAppList
    [void]$appForm.ShowDialog()
    
    # Recarregar lista de apps na interface principal apos fechar
    if ($script:BaseApps) {
        $script:Apps = @($script:BaseApps)
    } else {
        $script:Apps = @()
    }
    
    $configApps = Get-AllAppsFromConfig
    if ($configApps -and $configApps.Count -gt 0) {
        foreach ($app in $configApps) {
            if ($app.I -and $app.N) {
                $exists = $script:Apps | Where-Object { $_.I -eq $app.I }
                if (-not $exists) {
                    $script:Apps += $app
                }
            }
        }
    }
    
    # Recarregar modo de instalacao se estiver ativo
    if ($script:CurrentMode -eq "Install") {
        Show-InstallMode
    } elseif ($script:CurrentMode -eq "InstallOffline") {
        Show-InstallOfflineMode
    }
}

# ============================================
# FUNCOES DE NAVEGACAO DA SIDEBAR
# ============================================
function Show-SidebarMenu {
    $SidebarSectionPrimary.Visible = $true
    $SidebarSectionContext.Visible = $false
    $SidebarSectionUtility.Visible = $true

    # Mostrar menu principal
    $BtnModeInstall.Visible = $true
    $BtnModeUpdate.Visible = $true
    $BtnModeRemove.Visible = $true
    $BtnModeSystem.Visible = $true
    $BtnModePrinters.Visible = $true
    $BtnModeStorage.Visible = $true
    
    # Ocultar estado ativo
    $BtnBack.Visible = $false
    $BtnAll.Visible = $false
    $BtnNone.Visible = $false
    $BtnSearchOnline.Visible = $false
    
    # Ocultar submenu Sistema
    $BtnWinUpdates.Visible = $false
    $BtnDrivers.Visible = $false
    $BtnActivator.Visible = $false
    $BtnLocalAccount.Visible = $false
    $BtnScripts.Visible = $false
    
    # Ocultar submenu Impressoras
    $BtnPrinterEpsonSC.Visible = $false
    $BtnPrinterCanonG3160.Visible = $false
    $BtnPrinterCanonG2060.Visible = $false
    $BtnPrinterElginL42.Visible = $false
    $BtnPrinterArgoxOS.Visible = $false

    # Ocultar submenu Cache
    $BtnStorageWinget.Visible = $false
    $BtnStorageApps.Visible = $false
    
    # Ocultar controles do footer
    $LblCount.Visible = $false
    $BtnAction.Visible = $false
    
    # Resetar cores dos botoes de modo
    $BtnModeInstall.BackColor = $SidebarBtn
    $BtnModeUpdate.BackColor = $SidebarBtn
    $BtnModeRemove.BackColor = $SidebarBtn
    $BtnModeSystem.BackColor = $SidebarBtn
    $BtnModePrinters.BackColor = $SidebarBtn
    $BtnModeStorage.BackColor = $SidebarBtn
}

function Show-SidebarStorage {
    $SidebarSectionPrimary.Visible = $false
    $SidebarSectionContext.Visible = $true
    $SidebarSectionUtility.Visible = $true

    # Ocultar menu principal
    $BtnModeInstall.Visible = $false
    $BtnModeUpdate.Visible = $false
    $BtnModeRemove.Visible = $false
    $BtnModeSystem.Visible = $false
    $BtnModePrinters.Visible = $false
    $BtnModeStorage.Visible = $false
    
    # Ocultar estado ativo normal
    $BtnAll.Visible = $false
    $BtnNone.Visible = $false
    $BtnSearchOnline.Visible = $false
    
    # Ocultar outros submenus
    $BtnWinUpdates.Visible = $false
    $BtnDrivers.Visible = $false
    $BtnActivator.Visible = $false
    $BtnLocalAccount.Visible = $false
    $BtnScripts.Visible = $false
    $BtnPrinterEpsonSC.Visible = $false
    $BtnPrinterCanonG3160.Visible = $false
    $BtnPrinterCanonG2060.Visible = $false
    $BtnPrinterElginL42.Visible = $false
    $BtnPrinterArgoxOS.Visible = $false
    
    # Mostrar submenu Armazenamento
    $BtnBack.Visible = $true
    $BtnStorageWinget.Visible = $true
    $BtnStorageApps.Visible = $true
    
    # Atualizar layout
    Update-Layout
}

function Show-SidebarSystem {
    $SidebarSectionPrimary.Visible = $false
    $SidebarSectionContext.Visible = $true
    $SidebarSectionUtility.Visible = $true

    # Ocultar menu principal
    $BtnModeInstall.Visible = $false
    $BtnModeUpdate.Visible = $false
    $BtnModeRemove.Visible = $false
    $BtnModeSystem.Visible = $false
    $BtnModePrinters.Visible = $false
    $BtnModeStorage.Visible = $false
    
    # Ocultar estado ativo normal
    $BtnAll.Visible = $false
    $BtnNone.Visible = $false
    $BtnSearchOnline.Visible = $false
    
    # Ocultar submenu Impressoras
    $BtnPrinterEpsonSC.Visible = $false
    $BtnPrinterCanonG3160.Visible = $false
    $BtnPrinterCanonG2060.Visible = $false
    $BtnPrinterElginL42.Visible = $false
    $BtnPrinterArgoxOS.Visible = $false
    $BtnStorageWinget.Visible = $false
    $BtnStorageApps.Visible = $false
    
    # Mostrar submenu Sistema
    $BtnBack.Visible = $true
    $BtnWinUpdates.Visible = $true
    $BtnDrivers.Visible = $true
    $BtnActivator.Visible = $true
    $BtnLocalAccount.Visible = $true
    $BtnScripts.Visible = $true
    
    # Atualizar layout
    Update-Layout
}

function Show-SidebarPrinters {
    $SidebarSectionPrimary.Visible = $false
    $SidebarSectionContext.Visible = $true
    $SidebarSectionUtility.Visible = $true

    # Ocultar menu principal
    $BtnModeInstall.Visible = $false
    $BtnModeUpdate.Visible = $false
    $BtnModeRemove.Visible = $false
    $BtnModeSystem.Visible = $false
    $BtnModePrinters.Visible = $false
    $BtnModeStorage.Visible = $false
    
    # Ocultar estado ativo normal
    $BtnAll.Visible = $false
    $BtnNone.Visible = $false
    $BtnSearchOnline.Visible = $false
    
    # Ocultar submenu Sistema
    $BtnWinUpdates.Visible = $false
    $BtnDrivers.Visible = $false
    $BtnActivator.Visible = $false
    $BtnLocalAccount.Visible = $false
    $BtnScripts.Visible = $false
    $BtnStorageWinget.Visible = $false
    $BtnStorageApps.Visible = $false
    
    # Mostrar submenu Impressoras
    $BtnBack.Visible = $true
    $BtnPrinterEpsonSC.Visible = $true
    $BtnPrinterCanonG3160.Visible = $true
    $BtnPrinterCanonG2060.Visible = $true
    $BtnPrinterElginL42.Visible = $true
    $BtnPrinterArgoxOS.Visible = $true
    
    # Atualizar layout
    Update-Layout
}

function Show-SidebarActive {
    param([bool]$ShowEssentials = $true)

    $SidebarSectionPrimary.Visible = $false
    $SidebarSectionContext.Visible = $true
    $SidebarSectionUtility.Visible = $true
    
    # Ocultar menu principal
    $BtnModeInstall.Visible = $false
    $BtnModeUpdate.Visible = $false
    $BtnModeRemove.Visible = $false
    $BtnModeSystem.Visible = $false
    $BtnModePrinters.Visible = $false
    $BtnModeStorage.Visible = $false
    
    # Ocultar submenu Sistema
    $BtnWinUpdates.Visible = $false
    $BtnDrivers.Visible = $false
    $BtnActivator.Visible = $false
    $BtnLocalAccount.Visible = $false
    $BtnScripts.Visible = $false
    
    # Ocultar submenu Impressoras
    $BtnPrinterEpsonSC.Visible = $false
    $BtnPrinterCanonG3160.Visible = $false
    $BtnPrinterCanonG2060.Visible = $false
    $BtnPrinterElginL42.Visible = $false
    $BtnPrinterArgoxOS.Visible = $false
    $BtnStorageWinget.Visible = $false
    $BtnStorageApps.Visible = $false
    
    # Mostrar estado ativo
    $BtnBack.Visible = $true
    $BtnAll.Visible = $true
    $BtnNone.Visible = $true
    $BtnSearchOnline.Visible = $ShowEssentials  # Apenas no modo Instalar
    
    # Mostrar controles do footer
    $LblCount.Visible = $true
    $BtnAction.Visible = $true
    
    # Atualizar layout para reposicionar botoes corretamente
    Update-Layout
}

function Reset-ToMainMenu {
    $script:CurrentMode = $null
    Set-ViewContext -TitleText "" -SubtitleText "" -ShowHeader $false
    $WelcomeLabel.Visible = $true
    $SearchBox.Visible = $false
    $ListPanel.Visible = $false
    $ListPanel.Controls.Clear()
    $script:Checkboxes = @()
    $script:AppItems = @()
    
    # Limpar campo de busca
    Set-SearchBoxText -Text $SearchPlaceholder -ForeColor $TextDim
    
    Show-SidebarMenu
    Update-Layout
}

#endregion
