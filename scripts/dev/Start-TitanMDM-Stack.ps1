
#Requires -Version 5.1

<#
.SYNOPSIS
    TitanMDM - Inicio unificado de desarrollo.

.DESCRIPTION
    Inicia frontend Vite, Python Ponches y API .NET
    desde una sola consola PowerShell.

    WindowsAgent del equipo B permanece como servicio
    Windows independiente; nunca se ejecuta en el servidor.

    Para desarrollo local, no para producción/IIS.
#>

[CmdletBinding()]
param(
    [int]$FrontendPort = 3020,
    [int]$ApiPort = 8020,
    [int]$PythonPort = 8127,
    [switch]$EnableEntra
)

$ErrorActionPreference = 'Stop'

$root = [IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot '..\..')
)

$frontendDir = Join-Path $root 'src\frontend\titanmdm-web'
$frontendPackage = Join-Path $frontendDir 'package.json'
$nodeModules = Join-Path $frontendDir 'node_modules'

$ponchesLauncher = Join-Path $root 'scripts\ponches\Start-TitanLocal.ps1'

$logDir = Join-Path $root '.titan-local'
$frontendOut = Join-Path $logDir 'frontend.stdout.log'
$frontendErr = Join-Path $logDir 'frontend.stderr.log'

$frontendProcess = $null

function Test-TitanPort {
    param([int]$Port)

    $client = New-Object Net.Sockets.TcpClient

    try {
        $task = $client.ConnectAsync('127.0.0.1', $Port)
        if (!$task.Wait(500)) {
            return $false
        }
        return $client.Connected
    }
    catch {
        return $false
    }
    finally {
        $client.Dispose()
    }
}

function Wait-TitanHttp {
    param(
        [string]$Url,
        [int]$Seconds = 45
    )

    $limit = [DateTime]::UtcNow.AddSeconds($Seconds)

    while ([DateTime]::UtcNow -lt $limit) {
        if ($null -ne $script:frontendProcess) {
            $script:frontendProcess.Refresh()

            if ($script:frontendProcess.HasExited) {
                throw "El frontend se detuvo. Revisa $frontendErr"
            }
        }

        try {
            $response = Invoke-WebRequest `
                -Uri $Url `
                -UseBasicParsing `
                -TimeoutSec 3

            if ($response.StatusCode -ge 200 -and
                $response.StatusCode -lt 400) {
                return
            }
        }
        catch {
            Start-Sleep -Milliseconds 500
        }
    }

    throw "El frontend no respondió en $Seconds segundos: $Url"
}

function Stop-TitanFrontend {
    if ($null -eq $script:frontendProcess) {
        return
    }

    $script:frontendProcess.Refresh()

    if ($script:frontendProcess.HasExited) {
        return
    }

    # cmd.exe puede crear procesos hijos de Vite.
    # Terminamos únicamente el árbol del proceso
    # iniciado por este launcher.
    & taskkill.exe /PID $script:frontendProcess.Id /T /F 2>$null |
        Out-Null
}

try {
    Set-Location $root

    foreach ($item in @(
        $frontendPackage,
        $nodeModules,
        $ponchesLauncher
    )) {
        if (!(Test-Path $item)) {
            throw "Falta un requisito del entorno: $item"
        }
    }

    foreach ($port in @(
        $FrontendPort,
        $ApiPort,
        $PythonPort
    )) {
        if ($port -lt 1024 -or $port -gt 65535) {
            throw "Puerto inválido: $port"
        }
    }

    if (@(
        $FrontendPort,
        $ApiPort,
        $PythonPort
    ) | Select-Object -Unique |
        Measure-Object |
        Select-Object -ExpandProperty Count) {
        # La validación efectiva de duplicados aparece abajo.
    }

    if (
        $FrontendPort -eq $ApiPort -or
        $FrontendPort -eq $PythonPort -or
        $ApiPort -eq $PythonPort
    ) {
        throw 'Los puertos frontend, API y Python deben ser distintos.'
    }

    if (
        (Test-TitanPort $FrontendPort) -or
        (Test-TitanPort $ApiPort) -or
        (Test-TitanPort $PythonPort)
    ) {
        throw @'
Uno de los puertos solicitados ya está ocupado.

Cierra las instancias antiguas de Vite, .NET o Python
antes de iniciar el stack unificado.

No terminaremos procesos ajenos automáticamente.
'@
    }

    Get-Command npm.cmd -ErrorAction Stop |
        Out-Null

    Get-Command dotnet -ErrorAction Stop |
        Out-Null

    New-Item `
        -ItemType Directory `
        -Path $logDir `
        -Force |
        Out-Null

    Write-Host ''
    Write-Host '==========================================' -ForegroundColor Cyan
    Write-Host ' TITANMDM - STACK UNIFICADO' -ForegroundColor Cyan
    Write-Host '==========================================' -ForegroundColor Cyan
    Write-Host ''
    Write-Host "Repositorio: $root"
    Write-Host "Frontend: http://localhost:$FrontendPort"
    Write-Host "API:      http://localhost:$ApiPort"
    Write-Host "Python:   http://127.0.0.1:$PythonPort"
    Write-Host ''

    # ============================================================
    # FRONTEND: proceso hijo sin otra consola visible
    # ============================================================

    Write-Host '[1/3] Iniciando frontend...' -ForegroundColor Yellow

    $command = (
        "npm run dev -- --host 0.0.0.0 " +
        "--port $FrontendPort --strictPort"
    )

    $frontendProcess = Start-Process `
        -FilePath $env:ComSpec `
        -ArgumentList @(
            '/d',
            '/s',
            '/c',
            $command
        ) `
        -WorkingDirectory $frontendDir `
        -WindowStyle Hidden `
        -RedirectStandardOutput $frontendOut `
        -RedirectStandardError $frontendErr `
        -PassThru

    Wait-TitanHttp `
        -Url "http://127.0.0.1:$FrontendPort" `
        -Seconds 45

    Write-Host '[OK] Frontend funcionando.' -ForegroundColor Green

    # ============================================================
    # PONCHES + .NET: reutilizar launcher comprobado
    # ============================================================

    Write-Host '[2/3] Iniciando Python y verificando BioTime...' `
        -ForegroundColor Yellow

    Write-Host '[3/3] Iniciando TitanMDM API...' `
        -ForegroundColor Yellow

    $startParams = @{
        PythonPort = $PythonPort
        ApiPort = $ApiPort
    }

    if ($EnableEntra) {
        $startParams.EnableEntra = $true
    }

    # Este script queda ejecutándose hasta Ctrl+C.
    # Gestiona Python, clave interna, BioTime y API .NET.
    & $ponchesLauncher @startParams

    if (!$?) {
        throw 'El iniciador de Ponches/API informó un error.'
    }
}
finally {
    Write-Host ''
    Write-Host 'Cerrando componentes iniciados por TitanMDM...' `
        -ForegroundColor Yellow

    Stop-TitanFrontend

    Set-Location $root

    Write-Host 'Launcher finalizado.' -ForegroundColor Cyan
}
