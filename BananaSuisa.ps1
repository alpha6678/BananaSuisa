#Requires -Version 5.1
# ===========================================================================
# BANANASUISA - Script consolidado
# Gerado em: 09/04/2026 17:23:18
# Versao: 1.0.0
# ===========================================================================

# Versao embutida (espelha BananaSuisa_desenvolvimento/nucleo/versao.ps1)
$script:BananaSuisaVersao = '1.0.0'

#region Core_Bootstrap
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
#endregion

#region UI_Theme
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
#endregion

#region Features_Search
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
#endregion

#region UI_Layout
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
#endregion

#region Features_Catalog
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
#endregion

#region UI_Views
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
#endregion

#region Features_Actions
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
#endregion

#region App_Events
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
#endregion

