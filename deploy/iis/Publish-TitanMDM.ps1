#Requires -Version 5.1

<#
.SYNOPSIS
    TitanMDM - Genera el paquete de produccion (frontend + API).

.DESCRIPTION
    Produce en -OutputDir:
      web\   build de Vite (incluye web.config para IIS)
      api\   dotnet publish Release win-x64 (framework-dependent)
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

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $OutputDir = Join-Path $root "artifacts\titanmdm-$stamp"
}

$OutputDir = [IO.Path]::GetFullPath($OutputDir)
$webOut = Join-Path $OutputDir "web"
$apiOut = Join-Path $OutputDir "api"
$deployOut = Join-Path $OutputDir "deploy"

function Invoke-Step {
    param([string]$Title, [scriptblock]$Action)

    Write-Host "`n=== $Title ===" -ForegroundColor Cyan
    & $Action

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
