@echo off
:: BananaSuisa - launcher (executa o script consolidado)

set "SCRIPT_DIR=%~dp0"
set "SCRIPT_FILE=%SCRIPT_DIR%BananaSuisa.ps1"

if not exist "%SCRIPT_FILE%" (
    echo [X] Erro: %SCRIPT_FILE% nao encontrado.
    echo [!] Gere antes: .\bs.cmd build
    pause
    exit /b 1
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%SCRIPT_FILE%"
exit /b 0
