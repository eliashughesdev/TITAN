
#Requires -Version 5.1

<#
.SYNOPSIS
  TitanMDM PON-01: preflight del servicio Ponches.

.DESCRIPTION
  Valida rutas, Python, paquetes, configuración y ODBC.
  No inicia servicios.
  No modifica SQL Server ni relojes biométricos.
  No muestra ni almacena credenciales.
#>

[CmdletBinding()]
param(
    [switch]$InstallDependencies
)

$ErrorActionPreference = 'Stop'

$root = [IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot '..\..')
)

$backend = Join-Path $root 'integrations\ponches\backend'
$venv = Join-Path $backend '.venv'
$python = Join-Path $venv 'Scripts\python.exe'
$requirements = Join-Path $backend 'requirements.txt'
$envFile = Join-Path $backend '.env'
$launcher = Join-Path $root 'scripts\ponches\Start-TitanLocal.ps1'
$apiProject = Join-Path $root 'src\backend\TitanMDM.Api\TitanMDM.Api.csproj'

$failures = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()

function Write-Check {
    param(
        [string]$Title,
        [bool]$Ok
    )

    if ($Ok) {
        Write-Host "[OK] $Title" -ForegroundColor Green
    }
    else {
        Write-Host "[ERROR] $Title" -ForegroundColor Red
        $script:failures.Add($Title)
    }
}

Write-Host ''
Write-Host 'TITANMDM / PON-01 / PREFLIGHT' -ForegroundColor Cyan
Write-Host '========================================='

Write-Check 'Repositorio encontrado' (Test-Path $root)
Write-Check 'Backend Python encontrado' (Test-Path $backend)
Write-Check 'requirements.txt encontrado' (Test-Path $requirements)
Write-Check 'Gateway .NET encontrado' (Test-Path $apiProject)
Write-Check 'Iniciador integrado encontrado' (Test-Path $launcher)

if (!(Test-Path $python)) {
    if ($InstallDependencies) {
        $pyLauncher = Get-Command py -ErrorAction SilentlyContinue

        if (!$pyLauncher) {
            $failures.Add(
                'Python Launcher no está instalado.'
            )
        }
        else {
            Write-Host 'Creando entorno Python 3.12...' -ForegroundColor Cyan
            & py -3.12 -m venv $venv

            if ($LASTEXITCODE -ne 0) {
                $failures.Add(
                    'No se pudo crear el entorno virtual Python 3.12.'
                )
            }
        }
    }
    else {
        $failures.Add(
            'Falta .venv. Ejecuta con -InstallDependencies.'
        )
    }
}

if (Test-Path $python) {
    Write-Host ''
    Write-Host 'Versión Python:'
    & $python --version

    if ($LASTEXITCODE -ne 0) {
        $failures.Add('Python no pudo ejecutarse.')
    }
    elseif ($InstallDependencies) {
        Write-Host 'Instalando dependencias declaradas...' -ForegroundColor Cyan

        & $python -m pip install -r $requirements

        if ($LASTEXITCODE -ne 0) {
            $failures.Add(
                'Falló instalación de dependencias.'
            )
        }
    }

    if ($failures.Count -eq 0) {
        $importTest = @'
import fastapi
import uvicorn
import pyodbc
import pydantic_settings
import openpyxl
print("IMPORTS_OK")
'@

        $importTest | & $python -

        if ($LASTEXITCODE -ne 0) {
            $failures.Add(
                'Faltan dependencias de Python.'
            )
        }
    }
}

# Nunca imprimir el contenido de .env.
Write-Check 'Archivo de configuración .env' (Test-Path $envFile)

if (Test-Path $envFile) {
    $lines = Get-Content $envFile

    $requiredNames = @(
        'DB_SERVER',
        'DB_NAME',
        'DB_AUTH',
        'DB_DRIVER'
    )

    foreach ($name in $requiredNames) {
        $found = @(
            $lines | Where-Object {
                $_ -match "^\s*$name\s*=\s*\S+"
            }
        ).Count -gt 0

        if (!$found) {
            $warnings.Add(
                "Revisar variable $name en .env o entorno."
            )
        }
    }
}

# Los controladores ODBC son necesarios para SQL Server.
try {
    $odbc = @(
        Get-OdbcDriver |
        Where-Object {
            $_.Name -like 'ODBC Driver * for SQL Server'
        }
    )

    Write-Check 'Controlador ODBC SQL Server instalado' ($odbc.Count -gt 0)

    foreach ($driver in $odbc) {
        Write-Host "  Driver: $($driver.Name)"
    }
}
catch {
    $warnings.Add(
        'No se pudo verificar los controladores ODBC.'
    )
}

Write-Host ''
Write-Host '========================================='

foreach ($warning in $warnings) {
    Write-Host "[AVISO] $warning" -ForegroundColor Yellow
}

if ($failures.Count -gt 0) {
    Write-Host ''
    Write-Host 'PON-01: REQUISITOS INCOMPLETOS' -ForegroundColor Red

    foreach ($failure in $failures) {
        Write-Host " - $failure" -ForegroundColor Red
    }

    exit 1
}

Write-Host ''
Write-Host 'PON-01: PREFLIGHT CORRECTO' -ForegroundColor Green
Write-Host 'No se iniciaron servicios ni modificaron bases de datos.'
exit 0
