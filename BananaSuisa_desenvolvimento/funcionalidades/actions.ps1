#region [09-ACTIONS] Instalar, atualizar, remover, reparar e utilitarios

# ============================================
# FUNCOES WINGET INSTALL/REPAIR
# ============================================
# ============================================
# FUNCOES WINGET CACHE E INSTALL/REPAIR
# ============================================
function Update-WinGetCache {
    Write-Log "Iniciando atualizacao do cache de sistema WinGet..." -Type "Info"
    $cacheDir = $script:AppPaths.WinGetCache
    if (-not (Test-Path $cacheDir)) {
        try { New-Item -ItemType Directory -Path $cacheDir -Force -ErrorAction Stop | Out-Null }
        catch { Write-Log "Erro ao criar pasta de cache: $_" -Type "Error"; return }
    }

    try {
        # 1. Metadados do GitHub (WinGet)
        Update-LogProgress "Buscando release mais recente do WinGet no GitHub..." "[10%]"
        $releases = Invoke-RestMethod -Uri 'https://api.github.com/repos/microsoft/winget-cli/releases/latest' -ErrorAction Stop
        $msix = $releases.assets | Where-Object { $_.name -match '\.msixbundle$' -and $_.name -notmatch 'PreIndexed' } | Select-Object -First 1
        $license = $releases.assets | Where-Object { $_.name -match 'License.*\.xml$' } | Select-Object -First 1
        $version = $releases.tag_name -replace '^v', ''

        # 2. VCLibs
        Update-LogProgress "Baixando VCLibs..." "[0%]"
        $vclibsFile = Join-Path $cacheDir "vclibs_x64.appx"
        if (-not (Test-Path $vclibsFile)) {
            Invoke-WebDownload -Uri 'https://aka.ms/Microsoft.VCLibs.x64.14.00.Desktop.appx' -OutFile $vclibsFile -LogMessage "VCLibs"
            Write-Log "VCLibs salvo localmente." -Type "Success"
        }

        # 3. UI.Xaml (NuGet)
        Update-LogProgress "Buscando Microsoft.UI.Xaml no NuGet..." "[50%]"
        $packageId = "microsoft.ui.xaml"
        $indexUrl = "https://api.nuget.org/v3-flatcontainer/$packageId/index.json"
        $index = Invoke-RestMethod -Uri $indexUrl -ErrorAction Stop
        $latestXamlVer = ($index.versions | Where-Object { $_ -match '^\d+\.\d+\.\d+$' } | Sort-Object -Descending { [version]$_ } | Select-Object -First 1)
        $uixamlUrl = "https://api.nuget.org/v3-flatcontainer/$packageId/$latestXamlVer/$packageId.$latestXamlVer.nupkg"
        
        $xamlFile = Join-Path $cacheDir "uixaml_$latestXamlVer.nupkg"
        if (-not (Test-Path $xamlFile)) {
            Invoke-WebDownload -Uri $uixamlUrl -OutFile $xamlFile -LogMessage "UI.Xaml $latestXamlVer"
            Write-Log "UI.Xaml $latestXamlVer salvo localmente." -Type "Success"
        }

        # 4. WinGet MSIX e License
        Update-LogProgress "Baixando WinGet v$version..." "[80%]"
        $msixFile = Join-Path $cacheDir "winget_$version.msixbundle"
        $licenseFile = Join-Path $cacheDir "license_$version.xml"

        if (-not (Test-Path $msixFile) -and $msix) {
            Invoke-WebDownload -Uri $msix.browser_download_url -OutFile $msixFile -LogMessage "WinGet v$version"
            Write-Log "WinGet v$version salvo localmente." -Type "Success"
        }
        if (-not (Test-Path $licenseFile) -and $license) {
            Invoke-WebDownload -Uri $license.browser_download_url -OutFile $licenseFile -LogMessage "Licenca v$version"
        }

        # Criar arquivo de metadados do cache
        $metadata = @{
            LastUpdate = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
            WinGetVersion = $version
            UIXamlVersion = $latestXamlVer
            Files = @(
                Split-Path $vclibsFile -Leaf
                Split-Path $xamlFile -Leaf
                Split-Path $msixFile -Leaf
                Split-Path $licenseFile -Leaf
            )
        }
        $metadata | ConvertTo-Json | Out-File (Join-Path $cacheDir "cache_info.json") -Encoding UTF8

        Complete-LogProgress
        Write-Log "Cache de sistema atualizado com sucesso!" -Type "Success"
    } catch {
        Write-Log "Falha ao atualizar cache: $_" -Type "Error"
    }
}

function Install-WingetComplete {
    $cacheDir = $script:AppPaths.WinGetCache
    $useCache = Test-Path $cacheDir
    
    $script = @"
`$ErrorActionPreference = 'Stop'
`$ProgressPreference = 'SilentlyContinue'
`$Host.UI.RawUI.WindowTitle = 'Instalando Winget...'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

function Resolve-UIXamlDownloadUrl {
    param([string]`$PreferredVersion = '2.8.7')
    `$packageId = 'microsoft.ui.xaml'
    `$headers = @{ 'User-Agent' = 'PowerShell' }
    `$candidates = @(
        'https://api.nuget.org/v3-flatcontainer/`$packageId/`$PreferredVersion/`$packageId.`$PreferredVersion.nupkg',
        'https://www.nuget.org/api/v2/package/Microsoft.UI.Xaml/`$PreferredVersion'
    )
    try {
        `$indexUrl = 'https://api.nuget.org/v3-flatcontainer/`$packageId/index.json'
        `$index = Invoke-RestMethod -Uri `$indexUrl -Headers `$headers -ErrorAction Stop
        if (`$index -and `$index.versions) {
            foreach (`$ver in (`$index.versions | Where-Object { `$_ -match '^\d+\.\d+\.\d+$' } | Sort-Object -Descending { [version]`$_ })) {
                `$candidates += 'https://api.nuget.org/v3-flatcontainer/`$packageId/`$ver/`$packageId.`$ver.nupkg'
            }
        }
    } catch {}
    foreach (`$url in (`$candidates | Select-Object -Unique)) {
        try {
            `$head = Invoke-WebRequest -Method Head -Uri `$url -Headers `$headers -UseBasicParsing -TimeoutSec 12 -ErrorAction Stop
            if (`$head.StatusCode -in 200, 301, 302, 307, 308) { return `$url }
        } catch {}
    }
    return `$null
}

function Run-Install {
    Write-Host '--- Iniciando Etapas ---' -ForegroundColor Gray
    `$temp = "`$env:TEMP\WingetInstall"
    if (Test-Path `$temp) { Remove-Item `$temp -Recurse -Force -ErrorAction SilentlyContinue }
    New-Item -ItemType Directory -Path `$temp -Force | Out-Null
    `$cacheDir = '$cacheDir'

    # 1. VCLibs
    Write-Host '[1/5] Preparando VCLibs...' -ForegroundColor Yellow
    `$vclibsCache = Get-ChildItem `$cacheDir -Filter 'vclibs_x64.appx' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (`$vclibsCache) {
        Write-Host '      Usando cache local para VCLibs...' -ForegroundColor Cyan
        Copy-Item `$vclibsCache.FullName -Destination "`$temp\vclibs.appx" -Force
    } else {
        Invoke-WebRequest -Uri 'https://aka.ms/Microsoft.VCLibs.x64.14.00.Desktop.appx' -OutFile "`$temp\vclibs.appx" -UseBasicParsing
    }
    Add-AppxPackage -Path "`$temp\vclibs.appx" -ErrorAction SilentlyContinue

    # 2. UI.Xaml
    Write-Host '[2/5] Preparando UI.Xaml...' -ForegroundColor Yellow
    `$uixamlCache = Get-ChildItem `$cacheDir -Filter 'uixaml_*.nupkg' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (`$uixamlCache) {
        Copy-Item `$uixamlCache.FullName -Destination "`$temp\xaml.zip" -Force
    } else {
        `$uixamlUrl = Resolve-UIXamlDownloadUrl
        if (-not `$uixamlUrl) { throw 'Nao foi possivel localizar URL valida para Microsoft.UI.Xaml' }
        Invoke-WebRequest -Uri `$uixamlUrl -OutFile "`$temp\xaml.zip" -UseBasicParsing
    }
    Expand-Archive -Path "`$temp\xaml.zip" -DestinationPath "`$temp\xaml" -Force
    `$xamlAppx = Get-ChildItem "`$temp\xaml" -Recurse -Filter 'Microsoft.UI.Xaml*.appx' | Where-Object { `$_.FullName -match 'x64' -and `$_.FullName -notmatch 'arm' } | Select-Object -First 1
    if (`$xamlAppx) { Add-AppxPackage -Path `$xamlAppx.FullName -ErrorAction SilentlyContinue }

    # 3. Winget
    Write-Host '[3/5] Preparando Winget...' -ForegroundColor Yellow
    `$msixCache = Get-ChildItem `$cacheDir -Filter 'winget_*.msixbundle' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    `$licenseCache = Get-ChildItem `$cacheDir -Filter 'license_*.xml' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1

    if (`$msixCache) {
        Write-Host "      Usando cache local para Winget (`$(`$msixCache.Name))..." -ForegroundColor Cyan
        Copy-Item `$msixCache.FullName -Destination "`$temp\winget.msixbundle" -Force
        if (`$licenseCache) { Copy-Item `$licenseCache.FullName -Destination "`$temp\license.xml" -Force }
    } else {
        `$releases = Invoke-RestMethod -Uri 'https://api.github.com/repos/microsoft/winget-cli/releases/latest'
        `$msix = `$releases.assets | Where-Object { `$_.name -match '\.msixbundle$' -and `$_.name -notmatch 'PreIndexed' } | Select-Object -First 1
        `$license = `$releases.assets | Where-Object { `$_.name -match 'License.*\.xml$' } | Select-Object -First 1
        if (`$msix) { Invoke-WebRequest -Uri `$msix.browser_download_url -OutFile "`$temp\winget.msixbundle" -UseBasicParsing }
        if (`$license) { Invoke-WebRequest -Uri `$license.browser_download_url -OutFile "`$temp\license.xml" -UseBasicParsing }
    }

    # 4. Instalacao
    Write-Host '[4/5] Instalando Winget...' -ForegroundColor Yellow
    if (Test-Path "`$temp\license.xml") { 
        Add-AppxProvisionedPackage -Online -PackagePath "`$temp\winget.msixbundle" -LicensePath "`$temp\license.xml" -ErrorAction SilentlyContinue 
    }
    Add-AppxPackage -Path "`$temp\winget.msixbundle" -ForceApplicationShutdown
    
    # 5. PATH e Verificacao
    Write-Host '[5/5] Finalizando...' -ForegroundColor Yellow
    `$env:PATH = [System.Environment]::GetEnvironmentVariable('PATH', 'Machine') + ';' + [System.Environment]::GetEnvironmentVariable('PATH', 'User')
    Start-Sleep -Seconds 2
    if (Get-Command winget -ErrorAction SilentlyContinue) { 
        Write-Host 'Winget instalado com sucesso!' -ForegroundColor Green
    } else { 
        throw 'Winget nao detectado apos instalacao.' 
    }
}

Write-Host '=== INSTALACAO DO WINGET ===' -ForegroundColor Cyan
`$attempt = 1
`$max = 2
`$done = `$false

while (`$attempt -le `$max -and -not `$done) {
    try {
        Run-Install
        `$done = `$true
    } catch {
        Write-Host "`n[!] Erro na tentativa `$attempt: `$(`$_.Exception.Message)" -ForegroundColor Red
        if (`$attempt -lt `$max) {
            Write-Host '[*] Tentando limpeza para nova tentativa...' -ForegroundColor Yellow
            Get-AppxPackage Microsoft.DesktopAppInstaller -ErrorAction SilentlyContinue | Remove-AppxPackage -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 3
        } else {
            Write-Host '[X] Falha persistente na instalacao.' -ForegroundColor Red
            exit 1
        }
    }
    `$attempt++
}

Write-Host '`n=== PROCESSO CONCLUIDO ===' -ForegroundColor Green
Start-Sleep -Seconds 3
"@
    $file = "$env:TEMP\install_winget_$(Get-Random).ps1"
    $script | Out-File $file -Encoding UTF8
    $process = Start-Process powershell -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$file`"" -PassThru
    
    while (-not $process.HasExited) {
        [System.Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 100
    }

    if ($process.ExitCode -ne 0) {
        throw "Falha na instalacao do winget (codigo $($process.ExitCode))"
    }
}

function Repair-WingetComplete {
    Write-Log "Iniciando reparo do Winget..." -Type "Progress"
    $cacheDir = $script:AppPaths.WinGetCache
    $script = @"
function Resolve-UIXamlDownloadUrl {
    param([string]$PreferredVersion = "2.8.7")
    $packageId = "microsoft.ui.xaml"
    $headers = @{ "User-Agent" = "PowerShell" }
    $candidates = @(
        "https://api.nuget.org/v3-flatcontainer/$packageId/$PreferredVersion/$packageId.$PreferredVersion.nupkg",
        "https://www.nuget.org/api/v2/package/Microsoft.UI.Xaml/$PreferredVersion"
    )

    try {
        $indexUrl = "https://api.nuget.org/v3-flatcontainer/$packageId/index.json"
        $index = Invoke-RestMethod -Uri $indexUrl -Headers $headers -ErrorAction Stop
        if ($index -and $index.versions) {
            foreach ($ver in ($index.versions | Where-Object { $_ -match '^\d+\.\d+\.\d+$' } | Sort-Object -Descending { [version]$_ })) {
                $candidates += "https://api.nuget.org/v3-flatcontainer/$packageId/$ver/$packageId.$ver.nupkg"
            }
            $candidates = $candidates | Select-Object -Unique
        }
    } catch {}

    foreach ($url in $candidates) {
        try {
            $head = Invoke-WebRequest -Method Head -Uri $url -Headers $headers -UseBasicParsing -TimeoutSec 12 -ErrorAction Stop
            if ($head.StatusCode -in 200, 301, 302, 307, 308) { return $url }
        } catch {
            Write-Host "URL nao disponivel: $url" -ForegroundColor Yellow
        }
    }
    return $null
}

$Host.UI.RawUI.WindowTitle = 'Reparando Winget...'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Write-Host '=== REPARO DO WINGET ===' -ForegroundColor Cyan

# Verificar se AppXSvc esta disponivel
$appxSvc = Get-Service -Name "AppXSvc" -ErrorAction SilentlyContinue
$appxOK = $appxSvc -and $appxSvc.StartType -ne 'Disabled'
if (-not $appxOK) {
    Write-Host 'AVISO: AppXSvc nao disponivel (Windows modificado/LTSC)' -ForegroundColor Yellow
    Write-Host 'Algumas funcoes podem nao funcionar.' -ForegroundColor Yellow
}

$temp = "`$env:TEMP\WingetRepair"
New-Item -ItemType Directory -Path `$temp -Force | Out-Null
`$cacheDir = '$cacheDir'

Write-Host '[1/6] Limpando cache...' -ForegroundColor Yellow
@("`$env:LOCALAPPDATA\Packages\Microsoft.DesktopAppInstaller_8wekyb3d8bbwe\LocalCache","`$env:LOCALAPPDATA\Microsoft\Winget") | ForEach-Object { if (Test-Path `$_) { Remove-Item "`$_\*" -Recurse -Force -ErrorAction SilentlyContinue } }
Write-Host '      Cache limpo!' -ForegroundColor Green
    Write-Host '[2/6] Resetando sources...' -ForegroundColor Yellow
    winget source reset --force 2>&1 | Out-Null
    Write-Host '      Sources resetados!' -ForegroundColor Green
    Write-Host '[3/6] Reinstalando dependencias...' -ForegroundColor Yellow
    if (`$appxOK) {
        try {
            `$vclibsCache = Get-ChildItem `$cacheDir -Filter 'vclibs_x64.appx' -ErrorAction SilentlyContinue | Select-Object -First 1
            if (`$vclibsCache) {
                Write-Host '      Usando cache local para VCLibs...' -ForegroundColor Cyan
                Copy-Item `$vclibsCache.FullName -Destination "`$temp\vclibs.appx" -Force
            } else {
                Invoke-WebRequest -Uri 'https://aka.ms/Microsoft.VCLibs.x64.14.00.Desktop.appx' -OutFile "`$temp\vclibs.appx" -UseBasicParsing
            }
            Add-AppxPackage -Path "`$temp\vclibs.appx" -ErrorAction Stop
    
            `$uixamlCache = Get-ChildItem `$cacheDir -Filter 'uixaml_*.nupkg' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
            if (`$uixamlCache) {
                Write-Host '      Usando cache local para UI.Xaml...' -ForegroundColor Cyan
                Copy-Item `$uixamlCache.FullName -Destination "`$temp\xaml.zip" -Force
            } else {
                `$uixamlUrl = Resolve-UIXamlDownloadUrl
                if (-not `$uixamlUrl) { throw "Nao foi possivel localizar URL valida para Microsoft.UI.Xaml" }
                Invoke-WebRequest -Uri `$uixamlUrl -OutFile "`$temp\xaml.zip" -UseBasicParsing
            }
            Expand-Archive -Path "`$temp\xaml.zip" -DestinationPath "`$temp\xaml" -Force
            `$xaml = Get-ChildItem "`$temp\xaml" -Recurse -Filter "Microsoft.UI.Xaml*.appx" | Where-Object { `$_.FullName -match 'x64' -and `$_.FullName -notmatch 'arm' } | Select-Object -First 1
            if (`$xaml) { Add-AppxPackage -Path `$xaml.FullName -ErrorAction SilentlyContinue }
            Write-Host '      Dependencias instaladas!' -ForegroundColor Green
        } catch {
            Write-Host "      Erro: `$_" -ForegroundColor Red
            exit 1
        }
    } else {
    Write-Host '      Pulado (AppX indisponivel)' -ForegroundColor Yellow
}
Write-Host '[4/6] Reinstalando Winget...' -ForegroundColor Yellow
`$msixCache = Get-ChildItem `$cacheDir -Filter 'winget_*.msixbundle' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1

if (`$msixCache -and `$appxOK) {
    try {
        Write-Host "      Usando cache local para Winget (`$(`$msixCache.Name))..." -ForegroundColor Cyan
        Copy-Item `$msixCache.FullName -Destination "`$temp\winget.msixbundle" -Force
        Get-AppxPackage Microsoft.DesktopAppInstaller -ErrorAction SilentlyContinue | Remove-AppxPackage -ErrorAction SilentlyContinue
        Add-AppxPackage -Path "`$temp\winget.msixbundle" -ForceApplicationShutdown
        Write-Host '      Winget reinstalado!' -ForegroundColor Green
    } catch {
        Write-Host "      Erro: `$_" -ForegroundColor Red
        exit 1
    }
} elseif (`$appxOK) {
    `$releases = Invoke-RestMethod -Uri 'https://api.github.com/repos/microsoft/winget-cli/releases/latest'
    `$msix = `$releases.assets | Where-Object { `$_.name -match '\.msixbundle$' -and `$_.name -notmatch 'PreIndexed' } | Select-Object -First 1
    if (`$msix) {
        try {
            Invoke-WebRequest -Uri `$msix.browser_download_url -OutFile "`$temp\winget.msixbundle" -UseBasicParsing
            Get-AppxPackage Microsoft.DesktopAppInstaller -ErrorAction SilentlyContinue | Remove-AppxPackage -ErrorAction SilentlyContinue
            Add-AppxPackage -Path "`$temp\winget.msixbundle" -ForceApplicationShutdown
            Write-Host '      Winget reinstalado!' -ForegroundColor Green
        } catch {
            Write-Host "      Erro: `$_" -ForegroundColor Red
            exit 1
        }
    }
} elseif (-not `$appxOK) {
    Write-Host '      Pulado (AppX indisponivel)' -ForegroundColor Yellow
}
Write-Host '[5/6] Resetando Store...' -ForegroundColor Yellow
Start-Process 'wsreset.exe' -Wait -WindowStyle Hidden -ErrorAction SilentlyContinue
Write-Host '      Store resetada!' -ForegroundColor Green
Write-Host '[6/6] Verificando...' -ForegroundColor Yellow
`$env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH", "User")
Start-Sleep -Seconds 2
if (Get-Command winget -ErrorAction SilentlyContinue) { Write-Host 'Winget funcionando!' -ForegroundColor Green; winget --version }
else { Write-Host 'Reinicie o computador.' -ForegroundColor Yellow }
Remove-Item `$temp -Recurse -Force -ErrorAction SilentlyContinue
Write-Host ''; Write-Host '=== CONCLUIDO ===' -ForegroundColor Green
Read-Host 'Pressione Enter para fechar'
"@
    $file = "$env:TEMP\repair_winget_$(Get-Random).ps1"
    $script | Out-File $file -Encoding UTF8
    $process = Start-Process powershell -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$file`"" -PassThru
    
    # Aguardar o processo sem travar a UI
    while (-not $process.HasExited) {
        [System.Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 100
    }

    if ($process.ExitCode -ne 0) {
        throw "Falha no reparo do winget (codigo $($process.ExitCode))"
    }
}

#endregion
