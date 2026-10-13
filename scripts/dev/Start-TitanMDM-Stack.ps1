
#Requires -Version 5.1

<#
.SYNOPSIS
    TitanMDM - Stack unificado de desarrollo.

.DESCRIPTION
    Inicia y supervisa:
      Frontend Vite     :3020
      Backend .NET      :8020
      Python / Ponches  :8127

    Python y .NET comparten una clave temporal.
    BioTimeDB no bloquea la disponibilidad de Helpdesk.

    Uso exclusivo de desarrollo.
#>

[CmdletBinding()]
param(
    [int]$FrontendPort = 3020,
    [int]$ApiPort = 8020,
    [int]$PythonPort = 8127,
    [switch]$EnableEntra
)

$ErrorActionPreference = "Stop"

$root = [IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot "..\..")
)

$frontendDir = Join-Path $root `
    "src\frontend\titanmdm-web"

$pythonDir = Join-Path $root `
    "integrations\ponches\backend"

$pythonExe = Join-Path $pythonDir `
    ".venv\Scripts\python.exe"

$apiProject = Join-Path $root `
    "src\backend\TitanMDM.Api\TitanMDM.Api.csproj"

$logDir = Join-Path $root ".titan-local"

$front = $null
$api = $null
$python = $null

$previousLocation = Get-Location
$environmentBackup = @{}

function Test-TitanListener {
    param([int]$Port)

    return [bool](
        Get-NetTCPConnection `
            -LocalPort $Port `
            -State Listen `
            -ErrorAction SilentlyContinue |
            Select-Object -First 1
    )
}

function Assert-TitanPortAvailable {
    param([int]$Port)

    if ($Port -lt 1024 -or $Port -gt 65535) {
        throw "Puerto invalido: $Port"
    }

    if (Test-TitanListener $Port) {
        $listener = Get-NetTCPConnection `
            -LocalPort $Port `
            -State Listen `
            -ErrorAction SilentlyContinue |
            Select-Object -First 1

        throw "Puerto ${Port} ocupado por PID $($listener.OwningProcess). Cierra esa instancia primero."
    }
}

function Stop-TitanChild {
    param(
        [System.Diagnostics.Process]$Process,
        [switch]$Tree
    )

    if ($null -eq $Process) {
        return
    }

    try {
        $Process.Refresh()

        if ($Process.HasExited) {
            return
        }

        if ($Tree) {
            & taskkill.exe `
                /PID $Process.Id `
                /T `
                /F 2>$null | Out-Null
        }
        else {
            Stop-Process `
                -Id $Process.Id `
                -ErrorAction SilentlyContinue
        }
    }
    catch {
        Write-Warning "No se pudo detener PID $($Process.Id)"
    }
}

function Show-TitanLog {
    param(
        [string]$Name,
        [int]$Lines = 25
    )

    $path = Join-Path $logDir $Name

    if (Test-Path $path) {
        Write-Host "`n=== $Name ===" -ForegroundColor Yellow
        Get-Content $path -Tail $Lines
    }
}

function Wait-TitanService {
    param(
        [string]$Name,
        [int]$Port,
        [System.Diagnostics.Process]$Process,
        [int]$Seconds = 90
    )

    $deadline = (Get-Date).AddSeconds($Seconds)

    while ((Get-Date) -lt $deadline) {
        $Process.Refresh()

        if ($Process.HasExited) {
            throw "$Name finalizo antes de iniciar. ExitCode=$($Process.ExitCode)"
        }

        if (Test-TitanListener $Port) {
            Write-Host "[OK] ${Name}:$Port" -ForegroundColor Green
            return $true
        }

        Start-Sleep -Milliseconds 800
    }

    throw "$Name no escucho en $Port durante $Seconds segundos."
}

function Set-TitanEnvironment {
    param(
        [string]$Name,
        [string]$Value
    )

    $environmentBackup[$Name] =
        [Environment]::GetEnvironmentVariable(
            $Name, "Process"
        )

    [Environment]::SetEnvironmentVariable(
        $Name, $Value, "Process"
    )
}

try {
    Set-Location $root

    # ========================================================
    # 1. PRECHECK
    # ========================================================

    Write-Host "`n======================================" `
        -ForegroundColor Cyan
    Write-Host " TITANMDM - STACK UNIFICADO" `
        -ForegroundColor Cyan
    Write-Host "======================================" `
        -ForegroundColor Cyan

    if (@(
        $FrontendPort,
        $ApiPort,
        $PythonPort
    ) | Select-Object -Unique |
        Measure-Object |
        Select-Object -ExpandProperty Count) {
        # Validacion de unicidad explicita abajo.
    }

    if (
        $FrontendPort -eq $ApiPort -or
        $FrontendPort -eq $PythonPort -or
        $ApiPort -eq $PythonPort
    ) {
        throw "Los tres puertos deben ser distintos."
    }

    foreach ($port in @(
        $FrontendPort,
        $ApiPort,
        $PythonPort
    )) {
        Assert-TitanPortAvailable $port
    }

    foreach ($path in @(
        $pythonExe,
        $apiProject,
        (Join-Path $frontendDir "package.json"),
        (Join-Path $frontendDir "node_modules")
    )) {
        if (!(Test-Path $path)) {
            throw "Requisito no encontrado: $path"
        }
    }

    $dotnet = (Get-Command dotnet -ErrorAction Stop).Source
    Get-Command npm.cmd -ErrorAction Stop | Out-Null

    New-Item -ItemType Directory `
        -Path $logDir -Force | Out-Null

    # ========================================================
    # 2. VALIDAR BACKEND ANTES DE INICIAR
    # ========================================================

    Write-Host "`n[PRECHECK] Compilando API..." `
        -ForegroundColor Yellow

    & $dotnet build $apiProject --nologo

    if ($LASTEXITCODE -ne 0) {
        throw "Build backend fallido. No se iniciaron servicios."
    }

    # ========================================================
    # 3. CLAVE TEMPORAL COMPARTIDA
    # ========================================================

    $keyBytes = New-Object byte[] 32

    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()

    try {
        $rng.GetBytes($keyBytes)
    }
    finally {
        $rng.Dispose()
    }

    $integrationKey =
        [Convert]::ToBase64String($keyBytes)

    Set-TitanEnvironment `
        "ASPNETCORE_ENVIRONMENT" "Development"

    Set-TitanEnvironment `
        "DOTNET_ENVIRONMENT" "Development"

    Set-TitanEnvironment `
        "EntraLogin__Enabled" $(if ($EnableEntra) {
            "true"
        } else {
            "false"
        })

    Set-TitanEnvironment `
        "Ponches__BaseUrl" "http://127.0.0.1:$PythonPort"

    Set-TitanEnvironment `
        "Ponches__IntegrationKey" $integrationKey

    Set-TitanEnvironment `
        "TITAN_PONCHES_INTEGRATION_KEY" $integrationKey

    Set-TitanEnvironment `
        "Ponches__TimeoutSeconds" "180"

    # ========================================================
    # 4. INICIAR PYTHON
    # ========================================================

    Write-Host "`n[1/3] Iniciando Python / Ponches..." `
        -ForegroundColor Cyan

    $python = Start-Process `
        -FilePath $pythonExe `
        -ArgumentList @(
            "-m",
            "uvicorn",
            "app.main:app",
            "--host",
            "127.0.0.1",
            "--port",
            "$PythonPort"
        ) `
        -WorkingDirectory $pythonDir `
        -RedirectStandardOutput (
            Join-Path $logDir "ponches.stdout.log"
        ) `
        -RedirectStandardError (
            Join-Path $logDir "ponches.stderr.log"
        ) `
        -PassThru

    try {
        Wait-TitanService `
            -Name "Python" `
            -Port $PythonPort `
            -Process $python `
            -Seconds 90 | Out-Null

        try {
            $health = Invoke-RestMethod `
                -Uri "http://127.0.0.1:$PythonPort/api/internal/titan/health" `
                -Headers @{
                    "X-Titan-Integration-Key" = $integrationKey
                } `
                -TimeoutSec 15

            Write-Host "[OK] Bridge Python autenticado." `
                -ForegroundColor Green

            if ($health.database -ne "online") {
                Write-Warning "BioTime no esta online. Helpdesk continuara."
            }
            else {
                Write-Host "[OK] BioTimeDB online." `
                    -ForegroundColor Green
            }
        }
        catch {
            Write-Warning (
                "Python escucha, pero la prueba del bridge " +
                "autenticado fallo: $($_.Exception.Message)"
            )
        }
    }
    catch {
        Write-Warning $_.Exception.Message
        Show-TitanLog "ponches.stderr.log"
        Write-Warning "Continuando con API y Frontend."
    }

    # ========================================================
    # 5. INICIAR API .NET
    # ========================================================

    Write-Host "`n[2/3] Iniciando Backend .NET..." `
        -ForegroundColor Cyan

    $api = Start-Process `
        -FilePath $dotnet `
        -ArgumentList @(
            "run",
            "--project",
            "`"$apiProject`"",
            "--no-build",
            "--no-launch-profile",
            "--urls",
            "http://0.0.0.0:$ApiPort"
        ) `
        -WorkingDirectory $root `
        -RedirectStandardOutput (
            Join-Path $logDir "api.stdout.log"
        ) `
        -RedirectStandardError (
            Join-Path $logDir "api.stderr.log"
        ) `
        -PassThru

    try {
        Wait-TitanService `
            -Name "API" `
            -Port $ApiPort `
            -Process $api `
            -Seconds 90 | Out-Null
    }
    catch {
        Show-TitanLog "api.stderr.log"
        Show-TitanLog "api.stdout.log"
        throw
    }

    # ========================================================
    # 6. INICIAR FRONTEND
    # ========================================================

    Write-Host "`n[3/3] Iniciando React / Vite..." `
        -ForegroundColor Cyan

    $viteCommand =
        "npm.cmd run dev -- --host 0.0.0.0 " +
        "--port $FrontendPort --strictPort"

    $front = Start-Process `
        -FilePath $env:ComSpec `
        -ArgumentList @(
            "/d",
            "/s",
            "/c",
            $viteCommand
        ) `
        -WorkingDirectory $frontendDir `
        -RedirectStandardOutput (
            Join-Path $logDir "frontend.stdout.log"
        ) `
        -RedirectStandardError (
            Join-Path $logDir "frontend.stderr.log"
        ) `
        -PassThru

    try {
        Wait-TitanService `
            -Name "Frontend" `
            -Port $FrontendPort `
            -Process $front `
            -Seconds 60 | Out-Null
    }
    catch {
        Show-TitanLog "frontend.stderr.log"
        throw
    }

    # ========================================================
    # 7. RESULTADO
    # ========================================================

    Write-Host "`n======================================" `
        -ForegroundColor Green

    Write-Host " TITANMDM INICIADO" `
        -ForegroundColor Green

    Write-Host "======================================" `
        -ForegroundColor Green

    Write-Host "Frontend : http://localhost:$FrontendPort"
    Write-Host "API      : http://localhost:$ApiPort"
    Write-Host "Ponches  : http://127.0.0.1:$PythonPort"

    Write-Host "`nPID Frontend: $($front.Id)"
    Write-Host "PID Backend : $($api.Id)"
    Write-Host "PID Python  : $($python.Id)"

    Write-Host "`nMantener esta consola abierta."
    Write-Host "Ctrl+C para cerrar el stack."

    # ========================================================
    # 8. SUPERVISION
    # ========================================================

    while ($true) {
        Start-Sleep -Seconds 5

        $api.Refresh()
        $front.Refresh()

        if ($api.HasExited) {
            Show-TitanLog "api.stderr.log"
            Show-TitanLog "api.stdout.log"
            throw "La API termino inesperadamente."
        }

        if ($front.HasExited) {
            Show-TitanLog "frontend.stderr.log"
            throw "Frontend terminado inesperadamente."
        }

        if ($python) {
            $python.Refresh()

            if ($python.HasExited) {
                Write-Warning (
                    "Python termino. " +
                    "Helpdesk y frontend continuaran."
                )

                $python = $null
            }
        }
    }
}
finally {
    Write-Host "`nDeteniendo componentes de esta sesion..." `
        -ForegroundColor Yellow

    Stop-TitanChild -Process $front -Tree
    Stop-TitanChild -Process $api
    Stop-TitanChild -Process $python

    foreach ($name in $environmentBackup.Keys) {
        [Environment]::SetEnvironmentVariable(
            $name,
            $environmentBackup[$name],
            "Process"
        )
    }

    Set-Location $previousLocation

    Write-Host "Stack finalizado." -ForegroundColor Cyan
}
