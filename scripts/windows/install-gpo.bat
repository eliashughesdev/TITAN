@echo off
setlocal EnableExtensions DisableDelayedExpansion

set "SCRIPT_DIR=%~dp0"
set "POWERSHELL=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
set "INSTALLER=%SCRIPT_DIR%Install-TitanMDMAgent-GPO.ps1"
set "CONFIG=%SCRIPT_DIR%config.json"

if not exist "%POWERSHELL%" (
    exit /b 20
)

if not exist "%INSTALLER%" (
    exit /b 21
)

if not exist "%CONFIG%" (
    exit /b 22
)

"%POWERSHELL%" ^
    -NoLogo ^
    -NoProfile ^
    -NonInteractive ^
    -ExecutionPolicy Bypass ^
    -WindowStyle Hidden ^
    -File "%INSTALLER%" ^
    -ConfigPath "%CONFIG%"

set "EXITCODE=%ERRORLEVEL%"

exit /b %EXITCODE%