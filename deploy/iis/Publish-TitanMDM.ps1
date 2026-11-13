#Requires -Version 5.1

<#
.SYNOPSIS
    TitanMDM - Genera el paquete de produccion (frontend + API).

.DESCRIPTION
    Produce en -OutputDir:
      web\   build de Vite (incluye web.config para IIS)
      api\   dotnet publish Release win-x64 (framework-dependent)
      ponches\ codigo Python de Ponches / Fiorella (sin .venv, .env ni datos)
      agent\ APK del agente Android (deploy\app-debug.apk)
      deploy\ scripts y plantillas de instalacion

    No toca IIS ni servicios: el paquete se copia al servidor y alli se
    ejecuta Install-TitanMDM.ps1.

.EXAMPLE
    .\deploy\iis\Publish-TitanMDM.ps1 -OutputDir C:\Releases\TitanMDM-1.0.0
#>

[CmdletBinding()]
param(
    [string]$OutputDir,
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$frontendDir = Join-Path $root "src\frontend\titanmdm-web"
$apiProject = Join-Path $root "src\backend\TitanMDM.Api\TitanMDM.Api.csproj"
$ponchesDir = Join-Path $root "integrations\ponches\backend"
$agentApk = Join-Path $root "deploy\app-debug.apk"

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $OutputDir = Join-Path $root "artifacts\titanmdm-$stamp"
}

$OutputDir = [IO.Path]::GetFullPath($OutputDir)
$webOut = Join-Path $OutputDir "web"
$apiOut = Join-Path $OutputDir "api"
$deployOut = Join-Path $OutputDir "deploy"
$ponchesOut = Join-Path $OutputDir "ponches"
$agentOut = Join-Path $OutputDir "agent"

function Invoke-Step {
    param([string]$Title, [scriptblock]$Action)

    Write-Host "`n=== $Title ===" -ForegroundColor Cyan

    # npm/dotnet escriben avisos en stderr; con la salida redirigida,
    # PowerShell 5.1 los convertiria en errores. Manda el codigo de salida.
    $global:LASTEXITCODE = 0
    $previous = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        & $Action 2>&1 | ForEach-Object { "$_" }
    }
    finally {
        $ErrorActionPreference = $previous
    }

    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
        throw "$Title fallo (ExitCode=$LASTEXITCODE)."
    }
}

if (Test-Path $OutputDir) {
    throw "El destino ya existe: $OutputDir. Use una carpeta nueva por release."
}

New-Item -ItemType Directory -Path $webOut, $apiOut, $deployOut -Force | Out-Null

# ------------------------------------------------------------
# FRONTEND
# ------------------------------------------------------------

Push-Location $frontendDir
try {
    if (-not (Test-Path "node_modules")) {
        Invoke-Step "npm ci" { & npm.cmd ci }
    }

    if (-not $SkipTests) {
        Invoke-Step "Frontend: verificacion de tipos" { & npm.cmd exec -- tsc -b --noEmit }
    }

    Invoke-Step "Frontend: build de produccion" { & npm.cmd run build }

    Copy-Item -Path (Join-Path $frontendDir "dist\*") -Destination $webOut -Recurse -Force
}
finally {
    Pop-Location
}

if (-not (Test-Path (Join-Path $webOut "web.config"))) {
    throw "dist no contiene web.config (debe existir en public\web.config)."
}

if (-not (Test-Path (Join-Path $webOut "index.html"))) {
    throw "dist no contiene index.html."
}

# ------------------------------------------------------------
# API
# ------------------------------------------------------------

$selfContainedValue = if ($SelfContained) { "true" } else { "false" }

Invoke-Step "API: dotnet publish $Configuration $Runtime" {
    & dotnet publish $apiProject `
        -c $Configuration `
        -r $Runtime `
        --self-contained $selfContainedValue `
        -o $apiOut `
        --nologo `
        -p:DebugType=None `
        -p:EnvironmentName=Production
}

# appsettings.Development.json nunca viaja a produccion.
Remove-Item (Join-Path $apiOut "appsettings.Development.json") -ErrorAction SilentlyContinue

Copy-Item (Join-Path $PSScriptRoot "appsettings.Production.json") (Join-Path $apiOut "appsettings.Production.json") -Force

if (-not (Test-Path (Join-Path $apiOut "TitanMDM.Api.exe"))) {
    throw "No se genero TitanMDM.Api.exe."
}

# ------------------------------------------------------------
# PONCHES / FIORELLA (Python)
# ------------------------------------------------------------
# Solo codigo: el .venv se crea en el servidor; .env (credenciales de
# BioTime) y data\ (estado local) nunca viajan en el paquete.

Write-Host "`n=== Ponches: codigo Python ===" -ForegroundColor Cyan
New-Item -ItemType Directory -Path $ponchesOut -Force | Out-Null
& robocopy.exe (Join-Path $ponchesDir "app") (Join-Path $ponchesOut "app") /E /XD __pycache__ /XF *.pyc /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy fallo copiando Ponches (codigo $LASTEXITCODE)." }
$global:LASTEXITCODE = 0
Copy-Item (Join-Path $ponchesDir "requirements.txt") $ponchesOut -Force

if (-not (Test-Path (Join-Path $ponchesOut "app\main.py"))) {
    throw "El paquete de Ponches no contiene app\main.py."
}

# ------------------------------------------------------------
# AGENTE ANDROID
# ------------------------------------------------------------

if (Test-Path $agentApk) {
    New-Item -ItemType Directory -Path $agentOut -Force | Out-Null
    Copy-Item $agentApk $agentOut -Force
}
else {
    Write-Warning "No existe $agentApk; el paquete no incluye el APK del agente."
}

# ------------------------------------------------------------
# DEPLOY
# ------------------------------------------------------------

Get-ChildItem $PSScriptRoot -File |
    Where-Object { $_.Name -ne "Publish-TitanMDM.ps1" } |
    Copy-Item -Destination $deployOut -Force

$commit = (& git -C $root rev-parse --short HEAD 2>$null)

@{
    product = "TitanMDM"
    builtAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    commit = "$commit"
    configuration = $Configuration
    runtime = $Runtime
    selfContained = [bool]$SelfContained
} | ConvertTo-Json | Set-Content -Path (Join-Path $OutputDir "release.json") -Encoding UTF8

Write-Host "`nPaquete listo: $OutputDir" -ForegroundColor Green
Write-Host "Siguiente paso (en el servidor, como Administrador):"
Write-Host "  .\deploy\Install-TitanMDM.ps1 -PackageDir `"$OutputDir`" -HostName titanmdm.empresa.local -SecretsFile C:\TitanMDM-Secrets\titanmdm.secrets.json"
