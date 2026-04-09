#Requires -Version 5.1

# BananaSuisa.ps1
# Consolidador principal do ambiente WinGet com UI auditada, busca protegida,
# instalacao resiliente do WinGet e catalogo saneado.
#
# INDICE RAPIDO
# [01-BOOT]        Inicializacao, contexto e constantes
# [02-LOG]         Log em memoria e tratamento global de erros
# [03-SYS]         Requisitos, DLLs, UWP e tema do sistema
# [04-UI-THEME]    Paleta visual, estilo e helpers visuais
# [05-SEARCH]      Busca, debounce e filtros anti-loop
# [06-DATA]        Catalogo auditado de aplicativos e mapeamentos
# [07-UI-LAYOUT]   Header, sidebar, conteudo, footer e layout responsivo
# [08-UI-VIEWS]    Modos de tela, navegação e formularios
# [09-ACTIONS]     Install, update, remove, repair e utilitarios
# [10-PRINTERS]    Drivers e instaladores de impressoras
# [11-EVENTS]      Eventos e inicializacao principal

#region [01-BOOT] Inicializacao, contexto e constantes

$script:LogEntries = [System.Collections.ArrayList]::new()

# Versao (modular: nucleo/versao.ps1; consolidado: definida no topo do script gerado)
if (-not $script:BananaSuisaVersao) {
    $versaoCandidates = @(
        (Join-Path $PSScriptRoot "versao.ps1")
        (Join-Path $PSScriptRoot "BananaSuisa_desenvolvimento\nucleo\versao.ps1")
    )
    foreach ($vp in $versaoCandidates) {
        if ($vp -and (Test-Path -LiteralPath $vp)) {
            try { . $vp; break } catch { }
        }
    }
}
if (-not $script:BananaSuisaVersao) { $script:BananaSuisaVersao = "0.0.0" }

# 1. Identificar ambiente e definir espaco de trabalho (Workspace)
$script:SelfPath = if ($script:BananaSuisaEntryPath) { $script:BananaSuisaEntryPath } elseif ($PSCommandPath) { $PSCommandPath } else { $MyInvocation.MyCommand.Path }

function Get-BananaSuisaProjectRoot {
    if ($script:BananaSuisaRoot) { return $script:BananaSuisaRoot }
    $recursos = "BananaSuisa_recursos"
    $legacyPayload = "payload"
    $candidates = [System.Collections.Generic.List[string]]::new()
    if ($PSScriptRoot) {
        [void]$candidates.Add($PSScriptRoot)
        $p1 = Split-Path -Parent $PSScriptRoot
        if ($p1) {
            [void]$candidates.Add($p1)
            $p2 = Split-Path -Parent $p1
            if ($p2) { [void]$candidates.Add($p2) }
        }
    }
    $selfParent = Split-Path -Parent $script:SelfPath
    if ($selfParent) { [void]$candidates.Add($selfParent) }
    foreach ($c in $candidates) {
        if (-not $c) { continue }
        if ((Test-Path (Join-Path $c $recursos)) -or (Test-Path (Join-Path $c $legacyPayload))) {
            return $c
        }
    }
    if ($PSScriptRoot) { return (Split-Path -Parent $PSScriptRoot) }
    return $selfParent
}

$projectRoot = Get-BananaSuisaProjectRoot
$script:ScriptDir = $projectRoot

$script:PayloadRoot = if (Test-Path (Join-Path $projectRoot "BananaSuisa_recursos")) {
    Join-Path $projectRoot "BananaSuisa_recursos"
} elseif (Test-Path (Join-Path $projectRoot "payload")) {
    Join-Path $projectRoot "payload"
} else {
    $projectRoot
}

$script:MemoryFolderName = "BananaSuisa_memoria"
$script:MemoryFolderLegacy = "BananaSuisa.Data"

# Memoria fixa dentro de BananaSuisa_recursos (ou pasta payload legada): um unico lugar junto ao projeto
$script:AppRoot = Join-Path $script:PayloadRoot $script:MemoryFolderName

# Migrar de locais antigos (%LOCALAPPDATA%, raiz do projeto) se a pasta nova ainda nao existir
if (-not (Test-Path $script:AppRoot)) {
    $fromOld = @(
        (Join-Path $env:LOCALAPPDATA $script:MemoryFolderName)
        (Join-Path $env:LOCALAPPDATA $script:MemoryFolderLegacy)
        (Join-Path $projectRoot $script:MemoryFolderName)
        (Join-Path $projectRoot $script:MemoryFolderLegacy)
    )
    foreach ($oldPath in $fromOld) {
        if (-not $oldPath) { continue }
        if (Test-Path -LiteralPath $oldPath) {
            try {
                Move-Item -LiteralPath $oldPath -Destination $script:AppRoot -Force -ErrorAction Stop
                break
            } catch { }
        }
    }
}

$script:UseWorkspace = $false

# 2. Estrutura de subpastas do Workspace (nomes em portugues no disco)
$script:AppPaths = @{
    Root       = $script:AppRoot
    Logs       = Join-Path $script:AppRoot "Registros"
    Data       = Join-Path $script:AppRoot "Dados"
    Profiles   = Join-Path $script:AppRoot "Perfis"
    Scripts    = Join-Path $script:AppRoot "ScriptsExtras"
    Temp       = Join-Path $script:AppRoot "Temporarios"
    Drivers    = Join-Path $script:AppRoot "DriversImpressoras"
    Installers = Join-Path $script:AppRoot "PacotesBaixados"
    WinGetCache = Join-Path $script:AppRoot "PacotesBaixados\WinGet"
}

# 3. Configuracao Inicial de Caminhos
# Log vai direto para a pasta de estado desde o inicio (cria a pasta se necessario)
$script:BootLogDir = Join-Path $script:AppRoot "Registros"
if (-not (Test-Path $script:BootLogDir)) {
    try { New-Item -ItemType Directory -Path $script:BootLogDir -Force -ErrorAction Stop | Out-Null } catch {}
}
$script:LogFilePath = Join-Path $script:BootLogDir "BananaSuisa.json"
$script:PayloadConfigPath = Join-Path $script:PayloadRoot "BananaSuisa.config.json"
$script:PayloadCatalogPaths = @{
    Install = Join-Path $script:PayloadRoot "referencia_winget_instalacao_estavel.json"
    Tech    = Join-Path $script:PayloadRoot "referencia_winget_ti_estavel.json"
}
$script:ConfigPath = Join-Path $script:AppPaths.Data "BananaSuisa.config.json"
$script:AuditCatalogPaths = @{
    Install = Join-Path $script:AppPaths.Data "referencia_winget_instalacao_estavel.json"
    Tech    = Join-Path $script:AppPaths.Data "referencia_winget_ti_estavel.json"
}

function Get-BananaSuisaWingetExe {
    $cmd = Get-Command winget.exe -ErrorAction SilentlyContinue
    if ($cmd -and $cmd.Source) { return $cmd.Source }
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "Microsoft\WindowsApps\winget.exe")
        if (${env:ProgramFiles(x86)}) { Join-Path ${env:ProgramFiles(x86)} "Microsoft\WindowsApps\winget.exe" }
        (Join-Path $env:ProgramFiles "Microsoft\WindowsApps\winget.exe")
    )
    foreach ($p in $candidates) {
        if ($p -and (Test-Path -LiteralPath $p)) { return $p }
    }
    return "winget"
}

#endregion
#region [02-LOG] Log em memoria e tratamento global

# Funcao de log estruturado (Memoria)
function Write-FileLog {
    param([string]$Message, [string]$Level = "INFO")
    
    $logEntry = @{
        Timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        Level     = $Level
        Message   = $Message
        User      = $env:USERNAME
    }
    
    # Armazenar objeto em memoria
    [void]$script:LogEntries.Add($logEntry)
}

# Funcao para salvar histórico em JSON (Automatico e Estruturado)
function Save-LogToFile {
    if (-not $script:LogFilePath -or $script:LogEntries.Count -eq 0) { return $false }
    
    try {
        $path = $script:LogFilePath
        $history = @()
        
        # 1. Tentar ler historico existente se o arquivo existir
        if (Test-Path $path) {
            try {
                $existingContent = Get-Content $path -Raw -ErrorAction SilentlyContinue
                if ($existingContent) {
                    $history = $existingContent | ConvertFrom-Json
                    if ($history -isnot [System.Collections.ArrayList]) {
                        $history = [System.Collections.ArrayList]::new($history)
                    }
                }
            } catch {
                # Se o arquivo estiver corrompido, começamos um novo
                $history = [System.Collections.ArrayList]::new()
            }
        } else {
            $history = [System.Collections.ArrayList]::new()
        }
        
        # 2. Adicionar logs da sessão atual (Cópia para evitar problemas de concorrência)
        $batch = $script:LogEntries.Clone()
        foreach ($entry in $batch) {
            [void]$history.Add($entry)
        }
        
        # 3. Limpeza de Segurança (Manter apenas os últimos 2000 registros para performance)
        if ($history.Count -gt 2000) {
            $history = $history | Select-Object -Last 2000
        }
        
        # 4. Salvar como JSON formatado
        $jsonContent = $history | ConvertTo-Json -Depth 5
        [System.IO.File]::WriteAllText($path, $jsonContent, [System.Text.Encoding]::UTF8)
        
        # 5. Limpar logs em memória já persistidos para evitar duplicidade no próximo flush
        $script:LogEntries.Clear()
        
        return $true
    } catch {
        Write-Host "[!] FALHA CRITICA NO LOG ($path): $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

# PRIMEIRO LOG - se isso nao aparecer, o problema e antes mesmo do PowerShell carregar
Write-FileLog "========================================" "INFO"
Write-FileLog "INICIANDO BananaSuisa v$($script:BananaSuisaVersao)" "INFO"
Write-FileLog "PowerShell: $($PSVersionTable.PSVersion)" "INFO"
Write-FileLog "OS: $([Environment]::OSVersion.VersionString)" "INFO"
Write-FileLog "ScriptDir: $script:ScriptDir" "INFO"
Write-FileLog "ProjectRoot: $projectRoot" "INFO"
Write-FileLog "PayloadRoot: $script:PayloadRoot" "INFO"
Write-FileLog "AppRoot: $script:AppRoot" "INFO"
Write-FileLog "LogFile: $script:LogFilePath" "INFO"
Write-FileLog "User: $env:USERNAME" "INFO"
foreach ($catalogName in $script:PayloadCatalogPaths.Keys) {
    $catalogPath = $script:PayloadCatalogPaths[$catalogName]
    if ($catalogPath -and (Test-Path $catalogPath)) {
        Write-FileLog "Catalogo-base detectado [$catalogName]: $catalogPath" "INFO"
    }
}
Write-FileLog "========================================" "INFO"

# Trap global para erros
trap {
    Write-FileLog "ERRO FATAL: $_" "ERROR"
    Write-FileLog "StackTrace: $($_.ScriptStackTrace)" "ERROR"
    Save-LogToFile | Out-Null
    break # Mudado de 'continue' para 'break' para realmente parar em erros fatais
}

$ErrorActionPreference = "Continue"

#endregion
# region [03-SYS] Requisitos, DLLs, UWP e tema do sistema

# ============================================
# AUTO-ELEVACAO - Solicitar UAC se necessario (OBRIGATORIO)
# ============================================
Write-FileLog "Verificando privilegios de administrador..." "INFO"
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
Write-FileLog "Status Administrador: $isAdmin" "INFO"
Save-LogToFile | Out-Null

if (-not $isAdmin) {
    Write-FileLog "Nao e admin, solicitando elevacao UAC..." "INFO"
    try {
        # Tentar elevar usando o script atual com argumentos robustos (Array)
        $argList = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$script:SelfPath`"")
        Start-Process powershell.exe -Verb RunAs -ArgumentList $argList -WindowStyle Hidden
        Write-FileLog "Elevacao solicitada com sucesso, encerrando instancia atual" "INFO"
        Save-LogToFile | Out-Null
        exit
    } catch {
        Write-FileLog "Falha na elevacao UAC ou usuario cancelou: $_" "ERROR"
        Save-LogToFile | Out-Null
        
        # Garantir carga minima para MessageBox em caso de erro na elevação
        try { Add-Type -AssemblyName System.Windows.Forms -ErrorAction SilentlyContinue } catch {}
        
        [void][System.Windows.Forms.MessageBox]::Show(
            "Este aplicativo requer privilegios de administrador para funcionar corretamente.`n`nPor favor, execute o PowerShell como Administrador ou aceite a solicitacao UAC.",
            "Privilegios Necessarios",
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Warning
        )
        exit
    }
}

Write-FileLog "Executando como Administrador" "INFO"
Save-LogToFile | Out-Null

# ============================================
# VERIFICACAO DE REQUISITOS DO SISTEMA
# ============================================
Write-FileLog "--- VERIFICANDO REQUISITOS DO SISTEMA ---" "INFO"

# 1. Verificar DLLs do .NET Framework
Write-FileLog "Verificando DLLs do .NET Framework..." "INFO"
$script:DllPaths = @{
    "System.Windows.Forms" = $null
    "System.Drawing" = $null
}

# Caminhos possiveis para as DLLs
$possiblePaths = @(
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319",
    "C:\Windows\Microsoft.NET\Framework\v4.0.30319",
    "C:\Windows\Microsoft.NET\assembly\GAC_MSIL"
)

$requiredDLLs = @(
    "System.Windows.Forms.dll",
    "System.Drawing.dll"
)

foreach ($dll in $requiredDLLs) {
    $found = $false
    foreach ($basePath in $possiblePaths) {
        $fullPath = Join-Path $basePath $dll
        if (Test-Path $fullPath) {
            Write-FileLog "DLL OK: $fullPath" "INFO"
            $dllName = $dll -replace "\.dll$", ""
            $script:DllPaths[$dllName] = $fullPath
            $found = $true
            break
        }
    }
    if (-not $found) {
        Write-FileLog "DLL NAO ENCONTRADA: $dll" "WARNING"
    }
}

# 2. Verificar versao do .NET Framework
Write-FileLog "Verificando .NET Framework..." "INFO"
try {
    $netVersion = Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" -Name Version -ErrorAction SilentlyContinue
    if ($netVersion) {
        Write-FileLog ".NET Framework Version: $($netVersion.Version)" "INFO"
    } else {
        Write-FileLog ".NET Framework 4.x NAO ENCONTRADO" "WARNING"
    }
    
    $netRelease = Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" -Name Release -ErrorAction SilentlyContinue
    if ($netRelease) {
        $releaseNum = $netRelease.Release
        $friendlyVersion = switch ([int]$releaseNum) {
            { $_ -ge 533320 } { "4.8.1" }
            { $_ -ge 528040 } { "4.8" }
            { $_ -ge 461808 } { "4.7.2" }
            { $_ -ge 461308 } { "4.7.1" }
            { $_ -ge 460798 } { "4.7" }
            { $_ -ge 394802 } { "4.6.2" }
            { $_ -ge 394254 } { "4.6.1" }
            { $_ -ge 393295 } { "4.6" }
            default { "4.5.x ou anterior" }
        }
        Write-FileLog ".NET Framework: $friendlyVersion (Release $releaseNum)" "INFO"
    }
} catch {
    Write-FileLog "Erro ao verificar .NET Framework: $_" "WARNING"
}

# 3. Verificar Servicos Essenciais do Windows
Write-FileLog "Verificando servicos essenciais..." "INFO"
$requiredServices = @(
    @{Name = "AppXSvc"; Desc = "AppX Deployment Service (necessario para WinGet)"}
    @{Name = "StateRepository"; Desc = "State Repository Service"}
    @{Name = "RpcSs"; Desc = "Remote Procedure Call (RPC)"}
    @{Name = "wuauserv"; Desc = "Windows Update"}
    @{Name = "cryptsvc"; Desc = "Cryptographic Services"}
)

$script:MissingServices = @()
foreach ($svc in $requiredServices) {
    try {
        $service = Get-Service -Name $svc.Name -ErrorAction SilentlyContinue
        if ($service) {
            $status = $service.Status
            if ($status -eq "Running") {
                Write-FileLog "Servico OK: $($svc.Desc) [$status]" "INFO"
            } else {
                Write-FileLog "Servico PARADO: $($svc.Desc) [$status]" "WARNING"
            }
        } else {
            Write-FileLog "Servico FALTANDO: $($svc.Desc)" "ERROR"
            $script:MissingServices += $svc.Name
        }
    } catch {
        Write-FileLog "Erro ao verificar servico $($svc.Name): $_" "WARNING"
    }
}

# 4. Verificar se AppXSvc esta disponivel ANTES de chamar Get-AppxPackage
# Em Windows modificados/LTSC, Get-AppxPackage pode causar crash do PowerShell
Write-FileLog "Verificando servico AppXSvc (critico para UWP)..." "INFO"
$script:AppXAvailable = $false
try {
    $appxSvc = Get-Service -Name "AppXSvc" -ErrorAction SilentlyContinue
    if (-not $appxSvc) {
        Write-FileLog "Servico AppXSvc NAO EXISTE - Windows modificado/LTSC detectado" "WARNING"
    } elseif ($appxSvc.StartType -eq 'Disabled') {
        Write-FileLog "Servico AppXSvc DESABILITADO" "WARNING"
    } else {
        Write-FileLog "Servico AppXSvc: $($appxSvc.Status) ($($appxSvc.StartType))" "INFO"
        $script:AppXAvailable = $true
    }
} catch {
    Write-FileLog "Erro ao verificar AppXSvc: $_" "WARNING"
}

# Verificar Componentes UWP apenas se AppXSvc estiver disponivel
if ($script:AppXAvailable) {
    Write-FileLog "Verificando componentes UWP..." "INFO"
    try {
        $vclibs = Get-AppxPackage -Name "Microsoft.VCLibs*" -ErrorAction SilentlyContinue
        if ($vclibs) {
            Write-FileLog "VCLibs OK: $($vclibs.Name) v$($vclibs.Version)" "INFO"
        } else {
            Write-FileLog "VCLibs NAO ENCONTRADO (necessario para WinGet)" "WARNING"
        }
    } catch {
        Write-FileLog "Erro ao verificar VCLibs: $_" "WARNING"
        $script:AppXAvailable = $false
    }

    if ($script:AppXAvailable) {
        try {
            $uixaml = Get-AppxPackage -Name "Microsoft.UI.Xaml*" -ErrorAction SilentlyContinue
            if ($uixaml) {
                Write-FileLog "UI.Xaml OK: $($uixaml.Name) v$($uixaml.Version)" "INFO"
            } else {
                Write-FileLog "UI.Xaml NAO ENCONTRADO" "WARNING"
            }
        } catch {
            Write-FileLog "Erro ao verificar UI.Xaml: $_" "WARNING"
        }

        try {
            $appInstaller = Get-AppxPackage -Name "Microsoft.DesktopAppInstaller" -ErrorAction SilentlyContinue
            if ($appInstaller) {
                Write-FileLog "App Installer OK: v$($appInstaller.Version)" "INFO"
            } else {
                Write-FileLog "App Installer NAO ENCONTRADO (WinGet nao disponivel)" "WARNING"
            }
        } catch {
            Write-FileLog "Erro ao verificar App Installer: $_" "WARNING"
        }
    }
} else {
    Write-FileLog "MODO LIMITADO: Pulando verificacao de componentes UWP" "WARNING"
}

# 5. Verificar CLR
Write-FileLog "CLR Version: $($PSVersionTable.CLRVersion)" "INFO"

Write-FileLog "--- FIM DA VERIFICACAO DE REQUISITOS ---" "INFO"

# ============================================
# CARREGAR ASSEMBLIES COM FALLBACK
# ============================================
Write-FileLog "Carregando System.Windows.Forms..." "INFO"
$formsLoaded = $false
try {
    Add-Type -AssemblyName System.Windows.Forms -ErrorAction Stop
    Write-FileLog "System.Windows.Forms carregado OK (metodo padrao)" "INFO"
    $formsLoaded = $true
} catch {
    Write-FileLog "Metodo padrao falhou: $_" "WARNING"
    # Fallback: tentar caminho direto
    if ($script:DllPaths["System.Windows.Forms"]) {
        try {
            Add-Type -LiteralPath $script:DllPaths["System.Windows.Forms"] -ErrorAction Stop
            Write-FileLog "System.Windows.Forms carregado OK (caminho direto)" "INFO"
            $formsLoaded = $true
        } catch {
            Write-FileLog "Caminho direto tambem falhou: $_" "ERROR"
        }
    }
    
    if (-not $formsLoaded) {
        # Ultimo recurso: tentar caminhos conhecidos
        $fallbackPaths = @(
            "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll",
            "C:\Windows\Microsoft.NET\Framework\v4.0.30319\System.Windows.Forms.dll"
        )
        foreach ($path in $fallbackPaths) {
            if (Test-Path $path) {
                try {
                    Add-Type -LiteralPath $path -ErrorAction Stop
                    Write-FileLog "System.Windows.Forms carregado OK (fallback: $path)" "INFO"
                    $formsLoaded = $true
                    break
                } catch {
                    Write-FileLog "Fallback falhou para $path : $_" "WARNING"
                }
            }
        }
    }
}

if (-not $formsLoaded) {
    Write-FileLog "FALHA CRITICA: Nao foi possivel carregar System.Windows.Forms" "ERROR"
    Write-FileLog "Este componente e essencial para a interface grafica" "ERROR"
    throw "System.Windows.Forms nao pode ser carregado. Verifique se o .NET Framework esta instalado corretamente."
}

Write-FileLog "Carregando System.Drawing..." "INFO"
$drawingLoaded = $false
try {
    Add-Type -AssemblyName System.Drawing -ErrorAction Stop
    Write-FileLog "System.Drawing carregado OK (metodo padrao)" "INFO"
    $drawingLoaded = $true
} catch {
    Write-FileLog "Metodo padrao falhou: $_" "WARNING"
    # Fallback: tentar caminho direto
    if ($script:DllPaths["System.Drawing"]) {
        try {
            Add-Type -LiteralPath $script:DllPaths["System.Drawing"] -ErrorAction Stop
            Write-FileLog "System.Drawing carregado OK (caminho direto)" "INFO"
            $drawingLoaded = $true
        } catch {
            Write-FileLog "Caminho direto tambem falhou: $_" "ERROR"
        }
    }
    
    if (-not $drawingLoaded) {
        # Ultimo recurso
        $fallbackPaths = @(
            "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Drawing.dll",
            "C:\Windows\Microsoft.NET\Framework\v4.0.30319\System.Drawing.dll"
        )
        foreach ($path in $fallbackPaths) {
            if (Test-Path $path) {
                try {
                    Add-Type -LiteralPath $path -ErrorAction Stop
                    Write-FileLog "System.Drawing carregado OK (fallback: $path)" "INFO"
                    $drawingLoaded = $true
                    break
                } catch {
                    Write-FileLog "Fallback falhou para $path : $_" "WARNING"
                }
            }
        }
    }
}

if (-not $drawingLoaded) {
    Write-FileLog "FALHA CRITICA: Nao foi possivel carregar System.Drawing" "ERROR"
    throw "System.Drawing nao pode ser carregado. Verifique se o .NET Framework esta instalado corretamente."
}

# ============================================
# TRATAMENTO DE EXCECOES NAO TRATADAS (Global)
# ============================================
[System.Windows.Forms.Application]::add_ThreadException({
    param($sender, $e)
    $msg = "EXCECAO DE THREAD (WinForms): $($e.Exception.Message)"
    Write-FileLog $msg "ERROR"
    Write-FileLog "StackTrace: $($e.Exception.StackTrace)" "ERROR"
    Save-LogToFile | Out-Null
    [System.Windows.Forms.MessageBox]::Show($msg, "Erro Fatal (UI)", "OK", "Error")
})

[AppDomain]::CurrentDomain.add_UnhandledException({
    param($sender, $e)
    $msg = "EXCECAO NAO TRATADA (AppDomain): $($e.ExceptionObject.Message)"
    Write-FileLog $msg "ERROR"
    Write-FileLog "StackTrace: $($e.ExceptionObject.StackTrace)" "ERROR"
    Save-LogToFile | Out-Null
    # Nao podemos mostrar MessageBox aqui confiavelmente se o dominio estiver caindo
})

# Resumo de problemas encontrados
if ($script:MissingServices.Count -gt 0) {
    Write-FileLog "ATENCAO: Servicos faltando: $($script:MissingServices -join ', ')" "WARNING"
    Write-FileLog "Alguns recursos podem nao funcionar corretamente" "WARNING"
}

# ============================================
# OFERECER INSTALACAO DE DEPENDENCIAS UWP FALTANTES
# ============================================
$script:MissingUWPComponents = @()

# Verificar componentes UWP apenas se AppX estiver disponivel (flag definida acima)
if ($script:AppXAvailable) {
    Write-FileLog "Verificando componentes UWP faltantes para instalacao..." "INFO"
    try {
        # Verificar VCLibs especifico
        $vclibs = Get-AppxPackage -Name "Microsoft.VCLibs.140.00.UWPDesktop" -ErrorAction SilentlyContinue
        if (-not $vclibs) { $script:MissingUWPComponents += "VCLibs" }
        
        # Verificar UI.Xaml 2.x
        $uixaml = Get-AppxPackage -Name "Microsoft.UI.Xaml.2.*" -ErrorAction SilentlyContinue
        if (-not $uixaml) { $script:MissingUWPComponents += "UI.Xaml" }
        
        # Verificar WinGet/App Installer
        $appInstaller = Get-AppxPackage -Name "Microsoft.DesktopAppInstaller" -ErrorAction SilentlyContinue
        if (-not $appInstaller) { $script:MissingUWPComponents += "WinGet" }
    } catch {
        Write-FileLog "Erro ao verificar componentes: $_" "WARNING"
    }
} else {
    Write-FileLog "==================================================" "WARNING"
    Write-FileLog "MODO LIMITADO: AppX/UWP indisponivel neste Windows" "WARNING"
    Write-FileLog "O script usara apenas winget CLI diretamente" "WARNING"
    Write-FileLog "Funcionalidades afetadas:" "WARNING"
    Write-FileLog "  - Instalacao de dependencias UWP" "WARNING"
    Write-FileLog "  - Verificacao de apps da Microsoft Store" "WARNING"
    Write-FileLog "  - Modo Remover: apps UWP nao serao listados" "WARNING"
    Write-FileLog "==================================================" "WARNING"
}

if ($script:MissingUWPComponents.Count -gt 0) {
    Write-FileLog "Componentes UWP faltando: $($script:MissingUWPComponents -join ', ')" "WARNING"
    
    try {
        $result = [System.Windows.Forms.MessageBox]::Show(
            "Foram detectados componentes faltando no sistema:`n`n$($script:MissingUWPComponents -join ', ')`n`nDeseja instalar automaticamente?`n`n(Isso pode levar alguns minutos)",
            "Dependencias Faltando",
            [System.Windows.Forms.MessageBoxButtons]::YesNo,
            [System.Windows.Forms.MessageBoxIcon]::Question
        )
        
        if ($result -eq [System.Windows.Forms.DialogResult]::Yes) {
            Write-FileLog "Usuario solicitou instalacao de dependencias" "INFO"
            try {
                Install-WingetComplete
                Write-FileLog "Instalacao de dependencias concluida" "SUCCESS"
                [System.Windows.Forms.MessageBox]::Show(
                    "Dependencias instaladas com sucesso!`n`nO aplicativo continuara normalmente.",
                    "Sucesso",
                    [System.Windows.Forms.MessageBoxButtons]::OK,
                    [System.Windows.Forms.MessageBoxIcon]::Information
                )
            } catch {
                Write-FileLog "Falha na instalacao de dependencias: $_" "WARNING"
                [System.Windows.Forms.MessageBox]::Show(
                    "Falha ao instalar automaticamente.`nUse o botao Instalar Winget para tentar novamente.",
                    "Instalacao falhou",
                    [System.Windows.Forms.MessageBoxButtons]::OK,
                    [System.Windows.Forms.MessageBoxIcon]::Warning
                )
            }
        } else {
            Write-FileLog "Usuario optou por nao instalar dependencias" "INFO"
        }
    } catch {
        Write-FileLog "Erro ao oferecer instalacao de dependencias: $_" "WARNING"
    }
}

Write-FileLog "Habilitando Visual Styles..." "INFO"
[System.Windows.Forms.Application]::EnableVisualStyles()

# Configurar TLS 1.2 para todas as conexoes HTTPS
Write-FileLog "Configurando TLS 1.2..." "INFO"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Write-FileLog "TLS 1.2 configurado OK" "INFO"

# ============================================
# OCULTAR JANELA DO CONSOLE
# ============================================
Add-Type -Name Window -Namespace Console -MemberDefinition '
[DllImport("Kernel32.dll")]
public static extern IntPtr GetConsoleWindow();
[DllImport("user32.dll")]
public static extern bool ShowWindow(IntPtr hWnd, Int32 nCmdShow);
'

# ============================================
# API DWM PARA TEMA DO SISTEMA
# ============================================
Write-FileLog "Carregando API DWM..." "INFO"
$script:DwmApiAvailable = $false
try {
    Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class DwmApi {
    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
}
"@ -ErrorAction Stop
    $script:DwmApiAvailable = $true
    Write-FileLog "API DWM carregada com sucesso" "INFO"
} catch {
    # DwmApi nao disponivel nesta versao do Windows
    $script:DwmApiAvailable = $false
    Write-FileLog "API DWM nao disponivel: $_" "WARNING"
}

function Get-SystemThemeIsDark {
    try {
        $theme = Get-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize" -Name "AppsUseLightTheme" -ErrorAction SilentlyContinue
        return ($theme.AppsUseLightTheme -eq 0)
    } catch {
        return $true  # Padrao: modo escuro
    }
}

function Set-WindowTheme {
    param([System.Windows.Forms.Form]$Form)
    
    if (-not $script:DwmApiAvailable) { return }
    
    try {
        $isDark = Get-SystemThemeIsDark
        $value = if ($isDark) { 1 } else { 0 }
        [DwmApi]::DwmSetWindowAttribute($Form.Handle, [DwmApi]::DWMWA_USE_IMMERSIVE_DARK_MODE, [ref]$value, 4) | Out-Null
    } catch {
        # Ignorar erro se DWM nao suportar este atributo
    }
}

function Hide-ConsoleWindow {
    $consolePtr = [Console.Window]::GetConsoleWindow()
    [Console.Window]::ShowWindow($consolePtr, 0) | Out-Null
}

Hide-ConsoleWindow

# ============================================
# VARIAVEIS GLOBAIS
# ============================================
Write-FileLog "Inicializando variaveis globais..." "INFO"
$script:Installing = $false
$script:CancelRequested = $false
$script:CurrentProcess = $null
$script:CurrentMode = $null  # "Install", "Update", "Remove"
$script:CurrentStage = "Waiting"
$script:AvailableUpdates = @()
$script:InstalledApps = @()
$script:Checkboxes = @()
$script:AppItems = @()
$script:InstallResults = @{
    Success = @()
    Failed = @()
    Skipped = @()
    RebootRequired = @()
}

# Dicionario de erros do Winget (codigos oficiais da Microsoft)
$script:WingetErrors = @{
    0           = @{ Message = "Operacao concluida com sucesso"; Type = "Success" }
    3010        = @{ Message = "Requer reinicializacao"; Type = "Warning" }
    # Erros gerais
    -1978335229 = @{ Message = "Comando falhou"; Type = "Error" }
    -1978335226 = @{ Message = "Execucao do instalador falhou"; Type = "Error" }
    -1978335224 = @{ Message = "Falha no download"; Type = "Error" }
    -1978335216 = @{ Message = "Nao aplicavel para este sistema"; Type = "Error" }
    -1978335215 = @{ Message = "Hash do arquivo nao confere"; Type = "Error" }
    -1978335212 = @{ Message = "Pacote nao encontrado"; Type = "Error" }
    -1978335210 = @{ Message = "Multiplos pacotes encontrados"; Type = "Warning" }  # Multiple packages found
    -1978335207 = @{ Message = "Requer privilegios de administrador"; Type = "Error" }
    -1978335189 = @{ Message = "Em uso ou recurso do Windows (tente fechar apps ou instalar via Configuracoes)"; Type = "Warning" }
    -1978335185 = @{ Message = "Comando de desinstalacao nao encontrado"; Type = "Error" }
    -1978335184 = @{ Message = "Falha ao executar desinstalador"; Type = "Warning" }  # May succeed anyway
    -1978335145 = @{ Message = "Falha na desinstalacao portable"; Type = "Error" }
    -1978335134 = @{ Message = "Ja instalado"; Type = "Info" }
    -1978335130 = @{ Message = "Falha em uma ou mais desinstalacoes"; Type = "Error" }
    # Erros de instalacao
    -1978334975 = @{ Message = "Aplicacao em uso"; Type = "Warning" }
    -1978334973 = @{ Message = "Arquivo em uso"; Type = "Warning" }
    -1978334971 = @{ Message = "Disco cheio"; Type = "Error" }
    -1978334970 = @{ Message = "Memoria insuficiente"; Type = "Error" }
    -1978334967 = @{ Message = "Requer reinicializacao para concluir"; Type = "Warning" }
    -1978334964 = @{ Message = "Cancelado pelo usuario"; Type = "Info" }
    -1978334963 = @{ Message = "Outra versao ja instalada"; Type = "Info" }
    -1978334962 = @{ Message = "Versao mais recente ja instalada"; Type = "Info" }
    -1978334961 = @{ Message = "Bloqueado por politica"; Type = "Error" }
    # Rede e instalador
    -2147012889 = @{ Message = "Falha no download ou na rede (verifique conexao e firewall)"; Type = "Error" }
    -805306369   = @{ Message = "Falha na instalacao (app em uso ou reinstale manualmente)"; Type = "Error" }
}

# Etapas de instalacao
$script:Stages = @{
    "Waiting"     = "Aguardando..."
    "Searching"   = "Buscando pacote..."
    "Downloading" = "Baixando..."
    "Installing"  = "Instalando..."
    "Completed"   = "Concluido!"
    "Failed"      = "Falhou"
}

# ============================================
# INICIALIZACAO DO WORKSPACE (Com Feedback no Footer)
# ============================================
function Initialize-Workspace {
    Write-FileLog "Workspace: Iniciando..." "INFO"
    Write-Log "Iniciando ambiente de trabalho BananaSuisa..." -Type "Info"
    
    $root = $script:AppPaths.Root
    $createdCount = 0
    $failedCount = 0

    # 1. Tentar criar/validar pasta raiz
    try {
        if (-not (Test-Path $root)) {
            $null = New-Item -ItemType Directory -Path $root -Force -ErrorAction Stop
            Write-Log "Diretorio raiz criado com sucesso: $root" -Type "Success"
            $createdCount++
        } else {
            Write-Log "Diretorio raiz detectado: $root" -Type "Info"
        }
        $script:UseWorkspace = $true
    } catch {
        Write-FileLog "FALHA ao criar Workspace Root: $($_.Exception.Message)" "ERROR"
        Write-Log "AVISO: Workspace nao pode ser criado em $root. Usando modo portatil." -Type "Warning"
        Write-Log "Erro: $($_.Exception.Message)" -Type "Error"
        $script:UseWorkspace = $false
        return
    }

    # 2. Criar subpastas
    Write-FileLog "Workspace: Criando subpastas..." "INFO"
    foreach ($key in ($script:AppPaths.Keys | Sort-Object)) {
        if ($key -eq "Root") { continue }
        $path = $script:AppPaths[$key]
        if (-not (Test-Path $path)) {
            try {
                $null = New-Item -ItemType Directory -Path $path -Force -ErrorAction Stop
                $createdCount++
            } catch {
                Write-FileLog "FALHA ao criar subpasta $key ($path): $($_.Exception.Message)" "ERROR"
                $failedCount++
            }
        }
    }

    if ($createdCount -gt 0) { Write-Log "Workspace: $createdCount novas pastas configuradas." -Type "Success" }
    
    # 3. Log - caminho ja definido no boot, apenas confirmar
    Write-FileLog "Workspace: Log ativo em $($script:LogFilePath)" "INFO"
    Save-LogToFile | Out-Null
    
    # 4. Sincronizar recursos editaveis do payload para a pasta de estado
    if ((Test-Path $script:PayloadConfigPath) -and (-not (Test-Path $script:ConfigPath))) {
        try {
            Copy-Item $script:PayloadConfigPath $script:ConfigPath -Force
            Write-Log "Configuracoes-base copiadas para Dados (memoria em BananaSuisa_recursos)." -Type "Success"
        } catch {
            Write-FileLog "FALHA ao copiar configuracao-base: $($_.Exception.Message)" "ERROR"
        }
    }

    foreach ($key in $script:PayloadCatalogPaths.Keys) {
        $sourceFile = $script:PayloadCatalogPaths[$key]
        $targetFile = $script:AuditCatalogPaths[$key]
        if (-not $sourceFile -or -not (Test-Path $sourceFile)) { continue }

        if (-not (Test-Path $targetFile)) {
            try {
                Copy-Item $sourceFile $targetFile -Force
                Write-Log "Catalogo-base $(Split-Path $sourceFile -Leaf) sincronizado." -Type "Success"
            } catch {
                Write-FileLog "FALHA ao sincronizar catalogo ${key}: $($_.Exception.Message)" "ERROR"
            }
        }
    }

    $leiaMePath = Join-Path $root "LEIA-ME.txt"
    if (-not (Test-Path $leiaMePath)) {
        $txt = @'
BananaSuisa — pasta de memoria (dentro de BananaSuisa_recursos)
================================================================

Fica em: BananaSuisa_recursos\BananaSuisa_memoria (junto ao projeto).

Aqui ficam configuracoes, catalogos em uso, o arquivo de log (JSON) e arquivos
baixados. Os JSONs na raiz de BananaSuisa_recursos sao apenas modelos; as copias
de trabalho ficam em Dados\.

Subpastas:
  Registros         — Arquivo BananaSuisa.json (log da sessao) e outros diagnosticos.
  Dados             — Configuracao e catalogos (copiados dos modelos na primeira vez).
  Perfis            — Perfis de aplicativos para instalacao em lote.
  ScriptsExtras     — Scripts auxiliares.
  Temporarios       — Arquivos temporarios (pode limpar com o app fechado).
  DriversImpressoras — Drivers de impressora para instalacao.
  PacotesBaixados   — Instaladores guardados; WinGet = cache do WinGet.

Para redefinir o app aos padroes, apague esta pasta (com o programa fechado) e
execute de novo — os arquivos-base serao copiados de BananaSuisa_recursos.
'@
        [System.IO.File]::WriteAllText($leiaMePath, $txt.TrimEnd(), [System.Text.UTF8Encoding]::new($false))
        Write-Log "Arquivo LEIA-ME.txt criado na pasta de memoria." -Type "Info"
    }
}

# ============================================
# CONFIGURACAO - Arquivo JSON
# ============================================
function Get-DefaultConfig {
    return @{
        version = "5.0"
        apps = @()
        profiles = @{
            "Caixa" = @{
                description = "PDV/Caixa basico"
                color = "#f59e0b"
                apps = @("Google.Chrome", "AnyDesk.AnyDesk")
            }
            "Retaguarda" = @{
                description = "Retaguarda supermercado"
                color = "#3b82f6"
                apps = @("Google.Chrome", "AnyDesk.AnyDesk", "7zip.7zip")
            }
        }
        defaultProfile = "Caixa"
        customApps = @()
        settings = @{
            followSystemTheme = $true
            autoCheckDependencies = $true
            showLogPanel = $true
            confirmBeforeInstall = $true
            autoAcceptAgreements = $true
        }
    }
}

function Get-AppConfig {
    $defaultConfig = Get-DefaultConfig
    $configSourcePath = if (Test-Path $script:ConfigPath) { $script:ConfigPath } elseif (Test-Path $script:PayloadConfigPath) { $script:PayloadConfigPath } else { $null }
    
    if ($configSourcePath) {
        try {
            Write-FileLog "Carregando arquivo de config: $configSourcePath" "INFO"
            $content = Get-Content $configSourcePath -Raw -Encoding UTF8
            $config = $content | ConvertFrom-Json
            
            # Garantir que todos os campos existam
            if (-not $config.apps) {
                $config | Add-Member -NotePropertyName "apps" -NotePropertyValue $defaultConfig.apps -Force
            }
            if (-not $config.profiles) {
                $config | Add-Member -NotePropertyName "profiles" -NotePropertyValue $defaultConfig.profiles -Force
            }
            if (-not $config.customApps) {
                $config | Add-Member -NotePropertyName "customApps" -NotePropertyValue @() -Force
            }
            if (-not $config.settings) {
                $config | Add-Member -NotePropertyName "settings" -NotePropertyValue $defaultConfig.settings -Force
            }
            if (-not $config.defaultProfile) {
                $config | Add-Member -NotePropertyName "defaultProfile" -NotePropertyValue "Caixa" -Force
            }
            
            # Calcular totais para log
            $appsCount = if ($config.apps) { @($config.apps).Count } else { 0 }
            $profilesCount = if ($config.profiles -is [PSCustomObject]) { 
                @($config.profiles.PSObject.Properties).Count 
            } else { 0 }
            $customAppsCount = if ($config.customApps) { @($config.customApps).Count } else { 0 }
            
            Write-FileLog "Config carregada: $appsCount apps, $profilesCount perfis, $customAppsCount customizados" "INFO"
            return $config
        } catch {
            Write-FileLog "Erro ao carregar config: $_" "WARNING"
            return $defaultConfig
        }
    }
    
    Write-FileLog "Arquivo de config nao existe, usando padrao" "INFO"
    return $defaultConfig
}

# Variavel global para armazenar config carregada
$script:AppConfig = $null

function Load-AppConfig {
    $script:AppConfig = Get-AppConfig
    return $script:AppConfig
}

function Get-ProfileApps {
    param([string]$ProfileName)
    
    if (-not $script:AppConfig) { Load-AppConfig }
    
    $profileData = $null
    if ($script:AppConfig.profiles -is [PSCustomObject]) {
        $profileData = $script:AppConfig.profiles.$ProfileName
    } elseif ($script:AppConfig.profiles -is [hashtable]) {
        $profileData = $script:AppConfig.profiles[$ProfileName]
    }
    
    if ($profileData -and $profileData.apps) {
        return @($profileData.apps)
    }
    return @()
}

function Get-AllProfiles {
    if (-not $script:AppConfig) { Load-AppConfig }
    
    $profiles = @()
    if ($script:AppConfig.profiles -is [PSCustomObject]) {
        $script:AppConfig.profiles.PSObject.Properties | ForEach-Object {
            $profiles += @{
                Name = $_.Name
                Description = $_.Value.description
                Color = $_.Value.color
                Apps = @($_.Value.apps)
            }
        }
    } elseif ($script:AppConfig.profiles -is [hashtable]) {
        foreach ($key in $script:AppConfig.profiles.Keys) {
            $profiles += @{
                Name = $key
                Description = $script:AppConfig.profiles[$key].description
                Color = $script:AppConfig.profiles[$key].color
                Apps = @($script:AppConfig.profiles[$key].apps)
            }
        }
    }
    return $profiles
}

function Get-AllAppsFromConfig {
    if (-not $script:AppConfig) { Load-AppConfig }
    
    $apps = @()
    
    # Adicionar apps do config
    if ($script:AppConfig.apps) {
        foreach ($app in $script:AppConfig.apps) {
            $apps += @{
                N = if ($app.name) { $app.name } elseif ($app.N) { $app.N } else { $app.id }
                I = if ($app.id) { $app.id } elseif ($app.I) { $app.I } else { "" }
                C = if ($app.category) { $app.category } elseif ($app.C) { $app.C } else { "Outros" }
                E = $false  # Nao essencial por padrao
            }
        }
    }
    
    # Adicionar customApps
    if ($script:AppConfig.customApps) {
        foreach ($app in $script:AppConfig.customApps) {
            $apps += @{
                N = if ($app.N) { $app.N } elseif ($app.name) { $app.name } else { $app.I }
                I = if ($app.I) { $app.I } elseif ($app.id) { $app.id } else { "" }
                C = if ($app.C) { $app.C } elseif ($app.category) { $app.category } else { "Online" }
                E = $false
            }
        }
    }
    
    return $apps
}

function Save-AppConfig {
    param($Config)
    
    try {
        $configDir = Split-Path -Parent $script:ConfigPath
        if (-not (Test-Path $configDir)) {
            New-Item -ItemType Directory -Path $configDir -Force | Out-Null
        }
        $Config | ConvertTo-Json -Depth 10 | Set-Content $script:ConfigPath -Encoding UTF8 -Force
        # Atualizar variavel global
        $script:AppConfig = $Config
        return $true
    } catch {
        Write-FileLog "Erro ao salvar config: $_" "ERROR"
        return $false
    }
}

function Add-CustomApp {
    param(
        [string]$AppName,
        [string]$AppId,
        [string]$Category = "Online"
    )
    
    if (-not $script:AppConfig) { Load-AppConfig }
    
    # Verificar se ja existe nos apps principais
    $existsInApps = $script:AppConfig.apps | Where-Object { 
        ($_.id -eq $AppId) -or ($_.I -eq $AppId) 
    }
    if ($existsInApps) {
        return $false  # Ja existe
    }
    
    # Verificar se ja existe nos customApps
    $existsInCustom = $script:AppConfig.customApps | Where-Object { 
        ($_.I -eq $AppId) -or ($_.id -eq $AppId) 
    }
    if ($existsInCustom) {
        return $false  # Ja existe
    }
    
    # Adicionar novo app
    $newApp = @{
        N = $AppName
        I = $AppId
        C = $Category
    }
    
    # Converter para array se necessario e adicionar
    $customApps = @($script:AppConfig.customApps)
    $customApps += $newApp
    $script:AppConfig.customApps = $customApps
    
    # Salvar
    return (Save-AppConfig -Config $script:AppConfig)
}

function Remove-CustomApp {
    param([string]$AppId)
    
    if (-not $script:AppConfig) { Load-AppConfig }
    
    # Remover do customApps
    $script:AppConfig.customApps = @($script:AppConfig.customApps | Where-Object { 
        ($_.I -ne $AppId) -and ($_.id -ne $AppId) 
    })
    
    return (Save-AppConfig -Config $script:AppConfig)
}

function Add-AppToConfig {
    param(
        [string]$AppName,
        [string]$AppId,
        [string]$Category = "Outros"
    )
    
    if (-not $script:AppConfig) { Load-AppConfig }
    
    # Verificar se ja existe
    $existsInApps = $script:AppConfig.apps | Where-Object { 
        ($_.id -eq $AppId) -or ($_.I -eq $AppId) 
    }
    if ($existsInApps) {
        return $false
    }
    
    # Adicionar novo app na lista principal
    $newApp = [PSCustomObject]@{
        id = $AppId
        name = $AppName
        category = $Category
    }
    
    $apps = @($script:AppConfig.apps)
    $apps += $newApp
    $script:AppConfig.apps = $apps
    
    return (Save-AppConfig -Config $script:AppConfig)
}

function Remove-AppFromConfig {
    param([string]$AppId)
    
    if (-not $script:AppConfig) { Load-AppConfig }
    
    # Remover da lista de apps
    $script:AppConfig.apps = @($script:AppConfig.apps | Where-Object { 
        ($_.id -ne $AppId) -and ($_.I -ne $AppId) 
    })
    
    # Remover dos perfis
    if ($script:AppConfig.profiles -is [PSCustomObject]) {
        $script:AppConfig.profiles.PSObject.Properties | ForEach-Object {
            if ($_.Value.apps) {
                $_.Value.apps = @($_.Value.apps | Where-Object { $_ -ne $AppId })
            }
        }
    }
    
    return (Save-AppConfig -Config $script:AppConfig)
}

# Funcao para extrair ID do Winget a partir de comando colado
function Extract-WingetId {
    param([string]$Text)
    
    $text = $Text.Trim()
    
    # Tentar extrair com --id= ou --id 
    if ($text -match '--id[=\s]+[''"]?([A-Za-z0-9._-]+)[''"]?') {
        return $matches[1]
    }
    
    # Tentar extrair com -i= ou -i 
    if ($text -match '-i[=\s]+[''"]?([A-Za-z0-9._-]+)[''"]?') {
        return $matches[1]
    }
    
    # Se parece ser apenas um ID (Publisher.App ou Publisher.App.SubApp)
    if ($text -match '^[A-Za-z0-9]+\.[A-Za-z0-9._-]+$') {
        return $text
    }
    
    # Tentar pegar o ultimo parametro que parece um ID
    if ($text -match '([A-Za-z0-9]+\.[A-Za-z0-9._-]+)') {
        return $matches[1]
    }
    
    return $null
}

# ============================================
# FUNCOES AUXILIARES
# ============================================
function Test-WingetInstalled {
    $wg = Get-BananaSuisaWingetExe
    if ($wg -ne "winget" -and (Test-Path -LiteralPath $wg)) { return $true }
    try { $null = Get-Command winget.exe -ErrorAction Stop; return $true }
    catch { return $false }
}

# Funcao para verificar se um app esta instalado (usado para verificacao pos-remocao)
function Test-AppInstalled {
    param([string]$AppId)
    
    try {
        if ($script:WinGetModuleAvailable) {
            # Metodo moderno: usar modulo PowerShell
            $pkg = Get-WinGetPackage -Id $AppId -MatchOption Equals -ErrorAction SilentlyContinue
            return ($null -ne $pkg -and $pkg.Count -gt 0)
        } else {
            # Fallback: verificar via winget list
            $output = & (Get-BananaSuisaWingetExe) list --id $AppId --exact --accept-source-agreements 2>&1 | Out-String
            # Se nao encontrar, retorna mensagem "No installed package"
            return ($output -notmatch "No installed package" -and $output -notmatch "Nenhum pacote instalado")
        }
    } catch {
        # Em caso de erro, assumir que ainda esta instalado (conservador)
        return $true
    }
}

# Funcao para encerrar processos de um app (forca bruta para tecnicos de TI)
function Stop-AppProcesses {
    param(
        [string]$AppId,
        [string]$AppName
    )
    
    $killed = 0
    
    # Extrair nome base do app a partir do ID ou nome
    # Ex: "7zip.7zip" -> "7z", "7zip"; "Brave.Brave" -> "brave"
    $searchTerms = @()
    
    # Adicionar partes do ID
    if ($AppId) {
        $parts = $AppId -split '\.'
        foreach ($part in $parts) {
            $cleanPart = $part -replace '[^a-zA-Z0-9]', ''
            if ($cleanPart.Length -ge 2) {
                $searchTerms += $cleanPart.ToLower()
            }
        }
    }
    
    # Adicionar nome do app
    if ($AppName) {
        $cleanName = ($AppName -split ' ')[0] -replace '[^a-zA-Z0-9]', ''
        if ($cleanName.Length -ge 2) {
            $searchTerms += $cleanName.ToLower()
        }
    }
    
    # Remover duplicatas
    $searchTerms = $searchTerms | Select-Object -Unique
    
    # Mapeamento de IDs conhecidos para nomes de processos
    $knownProcesses = @{
        "7zip"    = @("7z", "7zFM", "7zG")
        "brave"   = @("brave")
        "chrome"  = @("chrome")
        "firefox" = @("firefox")
        "vlc"     = @("vlc")
        "notepad" = @("notepad++")
        "vscode"  = @("Code")
        "discord" = @("Discord", "Update")
        "spotify" = @("Spotify")
        "steam"   = @("steam", "steamwebhelper")
        "obs"     = @("obs64", "obs32")
    }
    
    # Encontrar processos a matar
    $processesToKill = @()
    
    foreach ($term in $searchTerms) {
        # Verificar se temos mapeamento conhecido
        foreach ($key in $knownProcesses.Keys) {
            if ($term -match $key) {
                $processesToKill += $knownProcesses[$key]
            }
        }
        
        # Buscar processos pelo nome
        try {
            $found = Get-Process | Where-Object { 
                $_.ProcessName -match $term -or 
                $_.MainWindowTitle -match $term 
            } -ErrorAction SilentlyContinue
            
            if ($found) {
                $processesToKill += $found.ProcessName
            }
        } catch { }
    }
    
    # Remover duplicatas e processos do sistema
    $processesToKill = $processesToKill | Select-Object -Unique | Where-Object {
        $_ -notin @("explorer", "System", "svchost", "winget", "powershell", "pwsh", "cmd")
    }
    
    # Matar processos
    foreach ($procName in $processesToKill) {
        try {
            $procs = Get-Process -Name $procName -ErrorAction SilentlyContinue
            if ($procs) {
                $procs | Stop-Process -Force -ErrorAction SilentlyContinue
                $killed += $procs.Count
            }
        } catch { }
    }
    
    return $killed
}

# ============================================
# FALLBACK: DESINSTALACAO VIA REGISTRO (ESTILO REVO UNINSTALLER)
# ============================================
function Get-RegistryUninstallInfo {
    param(
        [string]$AppName,
        [string]$AppId
    )
    
    # Chaves de registro padrao de desinstalacao
    $registryPaths = @(
        "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall"
    )
    
    # Termos de busca
    $searchTerms = @()
    if ($AppName) { 
        $searchTerms += $AppName
        # Adicionar primeira palavra do nome
        $firstWord = ($AppName -split ' ')[0]
        if ($firstWord.Length -ge 2) { $searchTerms += $firstWord }
    }
    if ($AppId) {
        $searchTerms += $AppId
        # Adicionar partes do ID
        $parts = $AppId -split '\.'
        foreach ($part in $parts) {
            if ($part.Length -ge 2) { $searchTerms += $part }
        }
    }
    $searchTerms = $searchTerms | Select-Object -Unique
    
    $results = @()
    
    foreach ($regPath in $registryPaths) {
        if (-not (Test-Path $regPath)) { continue }
        
        try {
            $keys = Get-ChildItem $regPath -ErrorAction SilentlyContinue
            
            foreach ($key in $keys) {
                try {
                    $displayName = $key.GetValue("DisplayName")
                    if (-not $displayName) { continue }
                    
                    # Verificar se algum termo de busca corresponde
                    $matched = $false
                    foreach ($term in $searchTerms) {
                        if ($displayName -like "*$term*") {
                            $matched = $true
                            break
                        }
                    }
                    
                    if ($matched) {
                        $uninstallString = $key.GetValue("QuietUninstallString")
                        if (-not $uninstallString) {
                            $uninstallString = $key.GetValue("UninstallString")
                        }
                        
                        if ($uninstallString) {
                            $results += [PSCustomObject]@{
                                DisplayName = $displayName
                                UninstallString = $uninstallString
                                QuietUninstallString = $key.GetValue("QuietUninstallString")
                                InstallLocation = $key.GetValue("InstallLocation")
                                Publisher = $key.GetValue("Publisher")
                                Version = $key.GetValue("DisplayVersion")
                            }
                        }
                    }
                } catch { }
            }
        } catch { }
    }
    
    return $results
}

# Funcao para calcular argumentos silenciosos baseado no tipo de instalador
function Get-SilentUninstallArgs {
    param([string]$UninstallString)
    
    $uninstallLower = $UninstallString.ToLower()
    
    # MSI: MsiExec.exe /X{GUID}
    if ($uninstallLower -match "msiexec") {
        # Extrair o comando base e adicionar argumentos silenciosos
        if ($UninstallString -match '/[IX]\{?[A-Fa-f0-9-]+\}?') {
            $guid = $matches[0]
            return @{
                Executable = "msiexec.exe"
                Arguments = "$guid /qn /norestart"
            }
        }
        # Se ja tem o comando completo, apenas adicionar /qn
        return @{
            Executable = "msiexec.exe"
            Arguments = ($UninstallString -replace '(?i)msiexec\.exe\s*', '') + " /qn /norestart"
        }
    }
    
    # Extrair executavel e argumentos existentes
    $executable = $UninstallString
    $existingArgs = ""
    
    # Formato: "C:\path\uninstall.exe" /args
    if ($UninstallString -match '^"([^"]+)"\s*(.*)$') {
        $executable = $matches[1]
        $existingArgs = $matches[2]
    }
    # Formato: C:\path\uninstall.exe /args (sem aspas)
    elseif ($UninstallString -match '^(\S+\.exe)\s*(.*)$') {
        $executable = $matches[1]
        $existingArgs = $matches[2]
    }
    
    # Detectar tipo de instalador e adicionar argumentos silenciosos
    $silentArgs = ""
    
    # NSIS (Nullsoft)
    if ($uninstallLower -match "unins" -or (Test-Path $executable -ErrorAction SilentlyContinue)) {
        # Tentar varios argumentos comuns
        $silentArgs = "/S"
    }
    
    # Inno Setup
    if ($uninstallLower -match "uninst" -or $existingArgs -match "_\?=") {
        $silentArgs = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART"
    }
    
    # Se nao detectou, usar argumentos mais comuns
    if (-not $silentArgs) {
        $silentArgs = "/S /SILENT /VERYSILENT /NORESTART"
    }
    
    # Combinar argumentos
    $finalArgs = if ($existingArgs) { "$existingArgs $silentArgs" } else { $silentArgs }
    
    return @{
        Executable = $executable
        Arguments = $finalArgs.Trim()
    }
}

# Funcao de fallback para desinstalar via registro
function Invoke-RegistryUninstall {
    param(
        [string]$AppName,
        [string]$AppId
    )
    
    $uninstallInfo = Get-RegistryUninstallInfo -AppName $AppName -AppId $AppId
    
    if ($uninstallInfo.Count -eq 0) {
        return @{ Success = $false; Message = "Nenhum desinstalador encontrado no registro" }
    }
    
    $successCount = 0
    
    foreach ($info in $uninstallInfo) {
        try {
            # Se ja tem QuietUninstallString, usar diretamente
            $cmdToRun = $info.QuietUninstallString
            
            if (-not $cmdToRun) {
                # Calcular argumentos silenciosos
                $silentInfo = Get-SilentUninstallArgs -UninstallString $info.UninstallString
                
                if ($silentInfo.Executable -and (Test-Path $silentInfo.Executable -ErrorAction SilentlyContinue)) {
                    $cmdToRun = "`"$($silentInfo.Executable)`" $($silentInfo.Arguments)"
                } else {
                    # Usar UninstallString original com argumentos adicionais
                    $cmdToRun = "$($info.UninstallString) /S /SILENT /VERYSILENT /NORESTART"
                }
            }
            
            Write-Log "$AppName - Executando: $($info.DisplayName)" -Type "Info"
            
            # Executar desinstalador
            $psi = New-Object System.Diagnostics.ProcessStartInfo
            $psi.FileName = "cmd.exe"
            $psi.Arguments = "/c `"$cmdToRun`""
            $psi.UseShellExecute = $false
            $psi.CreateNoWindow = $true
            $psi.RedirectStandardOutput = $true
            $psi.RedirectStandardError = $true
            
            $proc = [System.Diagnostics.Process]::Start($psi)
            $proc.WaitForExit(60000)  # Timeout de 60 segundos
            
            if ($proc.ExitCode -eq 0 -or $proc.ExitCode -eq 3010) {
                $successCount++
            }
            
            [System.Windows.Forms.Application]::DoEvents()
            Start-Sleep -Milliseconds 500
            
        } catch {
            Write-Log "$AppName - Erro ao executar desinstalador: $_" -Type "Warning"
        }
    }
    
    # Verificar se foi removido
    Start-Sleep -Milliseconds 1000
    $stillInstalled = Test-AppInstalled -AppId $AppId
    
    if (-not $stillInstalled) {
        return @{ Success = $true; Message = "Removido via desinstalador do registro" }
    }
    
    if ($successCount -gt 0) {
        return @{ Success = $true; Message = "Desinstalador executado ($successCount)" }
    }
    
    return @{ Success = $false; Message = "Falha ao executar desinstalador" }
}

# ============================================
# MODULO MICROSOFT.WINGET.CLIENT
# ============================================
$script:WinGetModuleAvailable = $false

function Initialize-WinGetModule {
    if ($script:WinGetModuleAvailable) { return $true }
    
    $maxRetries = 1
    $attempt = 0
    
    while ($attempt -le $maxRetries) {
        try {
            # Verificar se o modulo ja esta carregado
            if (Get-Module -Name Microsoft.WinGet.Client) {
                $script:WinGetModuleAvailable = $true
                return $true
            }
            
            # Verificar se esta disponivel para importar
            if (Get-Module -ListAvailable -Name Microsoft.WinGet.Client) {
                Update-LogProgress "Carregando modulo WinGet..."
                Import-Module Microsoft.WinGet.Client -ErrorAction Stop
                Complete-LogProgress
                $script:WinGetModuleAvailable = $true
                return $true
            }
            
            # Tentar instalar o modulo
            Update-LogProgress "Preparando instalacao do modulo WinGet..."
            
            # Garantir que NuGet provider esta disponivel
            $nuget = Get-PackageProvider -Name NuGet -ListAvailable -ErrorAction SilentlyContinue
            if (-not $nuget -or $nuget.Version -lt [Version]"2.8.5.201") {
                Update-LogProgress "Instalando NuGet provider..."
                Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Force -Scope CurrentUser | Out-Null
            }
            
            # Instalar o modulo com progresso
            Update-LogProgress "Baixando modulo Microsoft.WinGet.Client..." "[0%]"
            
            # Usar job para monitorar progresso
            $job = Start-Job -ScriptBlock {
                Install-Module -Name Microsoft.WinGet.Client -Scope CurrentUser -Force -AllowClobber -ErrorAction Stop
            }
            
            $stages = @("Conectando...", "Baixando...", "Verificando...", "Instalando...")
            $stageIndex = 0
            $progress = 0
            
            while ($job.State -eq 'Running') {
                $progress = [Math]::Min(95, $progress + 5)
                $stage = $stages[$stageIndex % $stages.Count]
                Update-LogProgress "Instalando modulo WinGet: $stage" "[$progress%]"
                [System.Windows.Forms.Application]::DoEvents()
                Start-Sleep -Milliseconds 500
                $stageIndex++
            }
            
            $result = Receive-Job -Job $job -ErrorAction Stop
            Remove-Job -Job $job
            
            Update-LogProgress "Importando modulo WinGet..." "[99%]"
            Import-Module Microsoft.WinGet.Client -ErrorAction Stop
            
            Complete-LogProgress
            $script:WinGetModuleAvailable = $true
            Write-Log "Modulo Microsoft.WinGet.Client instalado com sucesso" -Type "Success"
            return $true
        } catch {
            $attempt++
            if ($attempt -le $maxRetries) {
                Write-Log "Falha na instalacao do modulo WinGet. Tentando limpeza e nova tentativa ($attempt/$maxRetries)..." -Type "Warning"
                try {
                    Uninstall-Module -Name Microsoft.WinGet.Client -Force -AllVersions -ErrorAction SilentlyContinue
                } catch {}
                Start-Sleep -Seconds 2
            } else {
                Complete-LogProgress
                Write-Log "Modulo WinGet nao disponivel apos tentativas, usando fallback: $_" -Type "Warning"
                $script:WinGetModuleAvailable = $false
                return $false
            }
        }
    }
}

# ============================================
# CORES DO TEMA
# ============================================
#endregion
