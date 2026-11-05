#Requires -Version 5.1

<#
.SYNOPSIS
    TitanMDM - Stack unificado de desarrollo.

.DESCRIPTION
    Inicia y supervisa desde una sola consola:
      Frontend Vite     :3020
      Backend .NET      :8020
      Python / Ponches  :8127

    - Python y .NET comparten una clave de integracion temporal.
    - BioTimeDB / Python no bloquean la disponibilidad de Helpdesk ni del API.
    - Todos los procesos hijos se asocian a un Job Object de Windows con
      KILL_ON_JOB_CLOSE: si la consola se cierra (incluso con la X), Windows
      termina el arbol completo y no quedan puertos huerfanos.
    - Si un puerto esta ocupado por un proceso huerfano de ESTE repositorio
      (Vite, TitanMDM.Api, uvicorn de Ponches) se recupera automaticamente.
      Procesos ajenos nunca se terminan.

    Uso exclusivo de desarrollo. Para produccion ver deploy\iis.

.EXAMPLE
    .\scripts\dev\Start-TitanMDM-Stack.ps1

.EXAMPLE
    .\scripts\dev\Start-TitanMDM-Stack.ps1 -SkipPython -SkipBuild
#>

[CmdletBinding()]
param(
    [int]$FrontendPort = 3020,
    [int]$ApiPort = 8020,
    [int]$PythonPort = 8127,
    [switch]$EnableEntra,
    [switch]$SkipPython,
    [switch]$SkipBuild,
    [switch]$NoReclaim
)

$ErrorActionPreference = "Stop"

$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$frontendDir = Join-Path $root "src\frontend\titanmdm-web"
$pythonDir = Join-Path $root "integrations\ponches\backend"
$pythonExe = Join-Path $pythonDir ".venv\Scripts\python.exe"
$apiProject = Join-Path $root "src\backend\TitanMDM.Api\TitanMDM.Api.csproj"
$logDir = Join-Path $root ".titan-local"

$front = $null
$api = $null
$python = $null
$jobHandle = [IntPtr]::Zero

$previousLocation = Get-Location
$environmentBackup = @{}

# ============================================================
# Job Object: garantiza que los hijos mueren con la consola.
# ============================================================

if (-not ("TitanMdmJob" -as [type])) {
    try {
        Add-Type -TypeDefinition @"
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

public static class TitanMdmJob
{
    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimits
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimits
    {
        public BasicLimits Basic;
        public IoCounters Io;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    private const uint KillOnJobClose = 0x2000;
    private const int ExtendedLimitInformation = 9;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr attributes, string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(
        IntPtr job, int infoClass, ref ExtendedLimits info, uint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    public static IntPtr Create()
    {
        IntPtr handle = CreateJobObject(IntPtr.Zero, null);
        if (handle == IntPtr.Zero) throw new Win32Exception();

        ExtendedLimits info = new ExtendedLimits();
        info.Basic.LimitFlags = KillOnJobClose;

        if (!SetInformationJobObject(handle, ExtendedLimitInformation,
                ref info, (uint)Marshal.SizeOf(typeof(ExtendedLimits))))
            throw new Win32Exception();

        return handle;
    }

    public static void Assign(IntPtr job, IntPtr process)
    {
        if (!AssignProcessToJobObject(job, process)) throw new Win32Exception();
    }
}
"@
    }
    catch {
        Write-Warning "Job Object no disponible: $($_.Exception.Message)"
    }
}

# ============================================================
# Utilidades
# ============================================================

function Get-TitanPortOwners {
    param([int]$Port)

    return @(
        Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty OwningProcess -Unique |
            Where-Object { $_ -gt 4 }
    )
}

function Test-TitanListener {
    param([int]$Port)

    return (Get-TitanPortOwners $Port).Count -gt 0
}

function Test-TitanOwnedProcess {
    param([int]$ProcessId)

    $info = Get-CimInstance Win32_Process -Filter "ProcessId=$ProcessId" -ErrorAction SilentlyContinue

    if ($null -eq $info) {
        return $false
    }

    $signature = "$($info.ExecutablePath) $($info.CommandLine)"

    if ([string]::IsNullOrWhiteSpace($signature)) {
        return $false
    }

    if ($signature.IndexOf($root, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        return $true
    }

    # El launcher del venv crea el interprete real como proceso hijo; su
    # linea de comandos no incluye la ruta del repositorio.
    return $signature -match "uvicorn\s+app\.main:app"
}

function Stop-TitanTree {
    param([int]$ProcessId)

    & taskkill.exe /PID $ProcessId /T /F 2>$null | Out-Null
}

function Resolve-TitanPort {
    param(
        [int]$Port,
        [string]$Label
    )

    if ($Port -lt 1024 -or $Port -gt 65535) {
        throw "Puerto invalido para ${Label}: $Port"
    }

    $owners = Get-TitanPortOwners $Port

    if ($owners.Count -eq 0) {
        return
    }

    foreach ($ownerId in $owners) {
        $owned = Test-TitanOwnedProcess $ownerId

        if (-not $owned -or $NoReclaim) {
            $name = (Get-Process -Id $ownerId -ErrorAction SilentlyContinue).ProcessName
            throw (
                "Puerto $Port ($Label) ocupado por PID $ownerId ($name). " +
                "No pertenece a este stack o se uso -NoReclaim; cierralo manualmente."
            )
        }

        Write-Warning "Puerto $Port ($Label): terminando instancia huerfana de TitanMDM PID $ownerId."
        Stop-TitanTree $ownerId
    }

    $deadline = (Get-Date).AddSeconds(15)

    while ((Test-TitanListener $Port) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 400
    }

    if (Test-TitanListener $Port) {
        throw "No se pudo liberar el puerto $Port ($Label)."
    }
}

function Add-TitanToJob {
    param([System.Diagnostics.Process]$Process)

    if ($jobHandle -eq [IntPtr]::Zero) {
        return
    }

    try {
        [TitanMdmJob]::Assign($jobHandle, $Process.Handle)
    }
    catch {
        Write-Warning "No se pudo asociar PID $($Process.Id) al Job Object: $($_.Exception.Message)"
    }
}

function Stop-TitanChild {
    param([System.Diagnostics.Process]$Process)

    if ($null -eq $Process) {
        return
    }

    try {
        $Process.Refresh()

        if (-not $Process.HasExited) {
            # Siempre por arbol: dotnet run -> TitanMDM.Api.exe,
            # venv python -> interprete real, cmd -> node.
            Stop-TitanTree $Process.Id
        }
    }
    catch {
        Write-Warning "No se pudo detener PID $($Process.Id)"
    }
}

function Show-TitanLog {
    param(
        [string]$Name,
        [int]$Lines = 30
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
        [string]$HealthUrl,
        [int]$Seconds = 90
    )

    $deadline = (Get-Date).AddSeconds($Seconds)

    while ((Get-Date) -lt $deadline) {
        $Process.Refresh()

        if ($Process.HasExited) {
            throw "$Name finalizo antes de iniciar. ExitCode=$($Process.ExitCode)"
        }

        if (Test-TitanListener $Port) {
            if ([string]::IsNullOrWhiteSpace($HealthUrl)) {
                Write-Host "[OK] ${Name}:$Port" -ForegroundColor Green
                return
            }

            try {
                Invoke-WebRequest -Uri $HealthUrl -UseBasicParsing -TimeoutSec 5 | Out-Null
                Write-Host "[OK] ${Name}:$Port ($HealthUrl)" -ForegroundColor Green
                return
            }
            catch {
                # Escuchando pero aun no listo.
            }
        }

        Start-Sleep -Milliseconds 800
    }

    throw "$Name no estuvo listo en $Port durante $Seconds segundos."
}

function Set-TitanEnvironment {
    param(
        [string]$Name,
        [string]$Value
    )

    if (-not $environmentBackup.ContainsKey($Name)) {
        $environmentBackup[$Name] = [Environment]::GetEnvironmentVariable($Name, "Process")
    }

    [Environment]::SetEnvironmentVariable($Name, $Value, "Process")
}

function Start-TitanChild {
    param(
        [string]$FilePath,
        [string[]]$Arguments,
        [string]$WorkingDirectory,
        [string]$LogName
    )

    $process = Start-Process `
        -FilePath $FilePath `
        -ArgumentList $Arguments `
        -WorkingDirectory $WorkingDirectory `
        -NoNewWindow `
        -RedirectStandardOutput (Join-Path $logDir "$LogName.stdout.log") `
        -RedirectStandardError (Join-Path $logDir "$LogName.stderr.log") `
        -PassThru

    Add-TitanToJob $process
    return $process
}

# ============================================================
# Ejecucion
# ============================================================

try {
    Set-Location $root

    Write-Host "`n======================================" -ForegroundColor Cyan
    Write-Host " TITANMDM - STACK UNIFICADO" -ForegroundColor Cyan
    Write-Host "======================================" -ForegroundColor Cyan
    Write-Host "Repositorio: $root"

    # --------------------------------------------------------
    # 1. PRECHECK
    # --------------------------------------------------------

    $ports = @($FrontendPort, $ApiPort, $PythonPort)

    if (@($ports | Select-Object -Unique).Count -ne $ports.Count) {
        throw "Los puertos Frontend, API y Python deben ser distintos."
    }

    $required = @(
        $apiProject,
        (Join-Path $frontendDir "package.json"),
        (Join-Path $frontendDir "node_modules")
    )

    foreach ($path in $required) {
        if (-not (Test-Path $path)) {
            throw "Requisito no encontrado: $path"
        }
    }

    if (-not $SkipPython -and -not (Test-Path $pythonExe)) {
        Write-Warning "No existe $pythonExe. Ponches se omitira (crea el venv e instala requirements.txt)."
        $SkipPython = $true
    }

    $dotnet = (Get-Command dotnet -ErrorAction Stop).Source
    Get-Command npm.cmd -ErrorAction Stop | Out-Null

    New-Item -ItemType Directory -Path $logDir -Force | Out-Null

    Resolve-TitanPort -Port $FrontendPort -Label "Frontend"
    Resolve-TitanPort -Port $ApiPort -Label "API"

    if (-not $SkipPython) {
        Resolve-TitanPort -Port $PythonPort -Label "Python"
    }

    if ("TitanMdmJob" -as [type]) {
        try {
            $jobHandle = [TitanMdmJob]::Create()
        }
        catch {
            Write-Warning "No se pudo crear el Job Object: $($_.Exception.Message)"
        }
    }

    # --------------------------------------------------------
    # 2. COMPILAR BACKEND ANTES DE INICIAR NADA
    # --------------------------------------------------------

    if (-not $SkipBuild) {
        Write-Host "`n[PRECHECK] Compilando API..." -ForegroundColor Yellow

        & $dotnet build $apiProject --nologo -v minimal

        if ($LASTEXITCODE -ne 0) {
            throw "Build backend fallido. No se iniciaron servicios."
        }
    }

    # --------------------------------------------------------
    # 3. VARIABLES Y CLAVE TEMPORAL COMPARTIDA
    # --------------------------------------------------------

    $keyBytes = New-Object byte[] 32
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()

    try {
        $rng.GetBytes($keyBytes)
    }
    finally {
        $rng.Dispose()
    }

    $integrationKey = [Convert]::ToBase64String($keyBytes)

    Set-TitanEnvironment "ASPNETCORE_ENVIRONMENT" "Development"
    Set-TitanEnvironment "DOTNET_ENVIRONMENT" "Development"
    Set-TitanEnvironment "EntraLogin__Enabled" $(if ($EnableEntra) { "true" } else { "false" })
    Set-TitanEnvironment "Ponches__BaseUrl" "http://127.0.0.1:$PythonPort"
    Set-TitanEnvironment "Ponches__IntegrationKey" $integrationKey
    Set-TitanEnvironment "Ponches__TimeoutSeconds" "180"
    Set-TitanEnvironment "TITAN_PONCHES_INTEGRATION_KEY" $integrationKey
    Set-TitanEnvironment "PYTHONIOENCODING" "utf-8"

    # --------------------------------------------------------
    # 4. PYTHON / PONCHES (opcional, no bloqueante)
    # --------------------------------------------------------

    if ($SkipPython) {
        Write-Host "`n[1/3] Python / Ponches omitido." -ForegroundColor DarkYellow
    }
    else {
        Write-Host "`n[1/3] Iniciando Python / Ponches..." -ForegroundColor Cyan

        $python = Start-TitanChild `
            -FilePath $pythonExe `
            -Arguments @("-m", "uvicorn", "app.main:app", "--host", "127.0.0.1", "--port", "$PythonPort") `
            -WorkingDirectory $pythonDir `
            -LogName "ponches"

        try {
            Wait-TitanService -Name "Python" -Port $PythonPort -Process $python -Seconds 90

            try {
                $health = Invoke-RestMethod `
                    -Uri "http://127.0.0.1:$PythonPort/api/internal/titan/health" `
                    -Headers @{ "X-Titan-Integration-Key" = $integrationKey } `
                    -TimeoutSec 15

                Write-Host "[OK] Bridge Python autenticado." -ForegroundColor Green

                if ($health.database -ne "online") {
                    Write-Warning "BioTimeDB no esta online. Helpdesk y MDM continuaran."
                }
                else {
                    Write-Host "[OK] BioTimeDB online." -ForegroundColor Green
                }
            }
            catch {
                Write-Warning "Python escucha, pero el bridge autenticado fallo: $($_.Exception.Message)"
            }
        }
        catch {
            Write-Warning $_.Exception.Message
            Show-TitanLog "ponches.stderr.log"
            Write-Warning "Continuando sin Ponches."
            Stop-TitanChild $python
            $python = $null
        }
    }

    # --------------------------------------------------------
    # 5. API .NET
    # --------------------------------------------------------

    Write-Host "`n[2/3] Iniciando Backend .NET..." -ForegroundColor Cyan

    $api = Start-TitanChild `
        -FilePath $dotnet `
        -Arguments @(
            "run", "--project", "`"$apiProject`"",
            "--no-build", "--no-launch-profile",
            "--urls", "http://0.0.0.0:$ApiPort"
        ) `
        -WorkingDirectory $root `
        -LogName "api"

    try {
        Wait-TitanService `
            -Name "API" `
            -Port $ApiPort `
            -Process $api `
            -HealthUrl "http://127.0.0.1:$ApiPort/api/health/live" `
            -Seconds 120
    }
    catch {
        Show-TitanLog "api.stderr.log"
        Show-TitanLog "api.stdout.log"
        throw
    }

    # --------------------------------------------------------
    # 6. FRONTEND
    # --------------------------------------------------------

    Write-Host "`n[3/3] Iniciando React / Vite..." -ForegroundColor Cyan

    $front = Start-TitanChild `
        -FilePath $env:ComSpec `
        -Arguments @("/d", "/s", "/c", "npm.cmd run dev -- --host 0.0.0.0 --port $FrontendPort --strictPort") `
        -WorkingDirectory $frontendDir `
        -LogName "frontend"

    try {
        Wait-TitanService -Name "Frontend" -Port $FrontendPort -Process $front -Seconds 90
    }
    catch {
        Show-TitanLog "frontend.stderr.log"
        Show-TitanLog "frontend.stdout.log"
        throw
    }

    # --------------------------------------------------------
    # 7. RESULTADO
    # --------------------------------------------------------

    Write-Host "`n======================================" -ForegroundColor Green
    Write-Host " TITANMDM INICIADO" -ForegroundColor Green
    Write-Host "======================================" -ForegroundColor Green
    Write-Host "Frontend : http://localhost:$FrontendPort"
    Write-Host "API      : http://localhost:$ApiPort  (health: /api/health/ready)"
    if ($python) { Write-Host "Ponches  : http://127.0.0.1:$PythonPort" }
    Write-Host "Logs     : $logDir"
    Write-Host "`nPID Frontend: $($front.Id)"
    Write-Host "PID Backend : $($api.Id)"
    if ($python) { Write-Host "PID Python  : $($python.Id)" }
    Write-Host "`nMantener esta consola abierta. Ctrl+C para cerrar el stack."

    # --------------------------------------------------------
    # 8. SUPERVISION
    # --------------------------------------------------------

    while ($true) {
        Start-Sleep -Seconds 5

        $api.Refresh()
        $front.Refresh()

        if ($api.HasExited) {
            Show-TitanLog "api.stderr.log"
            Show-TitanLog "api.stdout.log"
            throw "La API termino inesperadamente. ExitCode=$($api.ExitCode)"
        }

        if ($front.HasExited) {
            Show-TitanLog "frontend.stderr.log"
            throw "El frontend termino inesperadamente."
        }

        if ($python) {
            $python.Refresh()

            if ($python.HasExited) {
                Show-TitanLog "ponches.stderr.log" 15
                Write-Warning "Python termino. Helpdesk, MDM y frontend continuaran."
                $python = $null
            }
        }
    }
}
finally {
    Write-Host "`nDeteniendo componentes de esta sesion..." -ForegroundColor Yellow

    Stop-TitanChild $front
    Stop-TitanChild $api
    Stop-TitanChild $python

    foreach ($name in $environmentBackup.Keys) {
        [Environment]::SetEnvironmentVariable($name, $environmentBackup[$name], "Process")
    }

    Set-Location $previousLocation
    Write-Host "Stack finalizado." -ForegroundColor Cyan
}
