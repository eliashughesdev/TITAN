param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",

    [ValidateSet("win-x64")]
    [string]$Runtime = "win-x64",

    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = [System.IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot "..\..")
)

$solution = Join-Path $repoRoot "TitanMDM.slnx"
$packageBuilder = Join-Path `
    $PSScriptRoot `
    "Build-TitanMDMAgentPackage.ps1"

$artifactRoot = Join-Path `
    $repoRoot `
    "artifacts\windows-agent"

$packageRoot = Join-Path `
    $artifactRoot `
    "package"

$zipPath = Join-Path `
    $artifactRoot `
    "TitanMDM-WindowsAgent-x64.zip"

$agentExe = Join-Path `
    $packageRoot `
    "Agent\TitanMDM.WindowsAgent.exe"

$remoteHostExe = Join-Path `
    $packageRoot `
    "RemoteHost\TitanMDM.RemoteHost.exe"

$manifestPath = Join-Path `
    $artifactRoot `
    "W11-build-manifest.json"

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & dotnet @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw (
            "dotnet terminó con código " +
            $LASTEXITCODE +
            ": " +
            ($Arguments -join " ")
        )
    }
}

function Assert-File {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    if (!(Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Description no existe: $Path"
    }

    $file = Get-Item -LiteralPath $Path

    if ($file.Length -le 0) {
        throw "$Description está vacío: $Path"
    }

    return $file
}

if (!(Test-Path -LiteralPath $solution)) {
    throw "No se encontró TitanMDM.slnx en $repoRoot"
}

if (!(Test-Path -LiteralPath $packageBuilder)) {
    throw "No se encontró Build-TitanMDMAgentPackage.ps1"
}

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue

if ($null -eq $dotnet) {
    throw "Instala el SDK .NET requerido antes de construir W11."
}

Write-Host ""
Write-Host "W11 | TitanMDM Windows Agent" -ForegroundColor Cyan
Write-Host "Repositorio: $repoRoot"
Write-Host "Configuración: $Configuration"
Write-Host "Runtime: $Runtime"
Write-Host ""

Push-Location $repoRoot

try {
    Write-Host "[1/5] Compilando solución..." -ForegroundColor Yellow

    Invoke-DotNet @(
        "build",
        $solution,
        "-c",
        $Configuration
    )

    if (!$SkipTests) {
        Write-Host "[2/5] Ejecutando pruebas..." -ForegroundColor Yellow

        Invoke-DotNet @(
            "test",
            $solution,
            "-c",
            $Configuration,
            "--no-build"
        )
    }
    else {
        Write-Host "[2/5] Pruebas omitidas por -SkipTests." `
            -ForegroundColor Yellow
    }

    Write-Host "[3/5] Publicando Agent y RemoteHost..." `
        -ForegroundColor Yellow

    & $packageBuilder `
        -Configuration $Configuration `
        -Runtime $Runtime

    if ($LASTEXITCODE -ne 0) {
        throw "Falló Build-TitanMDMAgentPackage.ps1"
    }

    $agentFile = Assert-File `
        -Path $agentExe `
        -Description "TitanMDM.WindowsAgent.exe"

    $remoteHostFile = Assert-File `
        -Path $remoteHostExe `
        -Description "TitanMDM.RemoteHost.exe"

    $zipFile = Assert-File `
        -Path $zipPath `
        -Description "Paquete ZIP W11"

    Write-Host "[4/5] Verificando contenido del ZIP..." `
        -ForegroundColor Yellow

    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $archive = [System.IO.Compression.ZipFile]::OpenRead(
        $zipPath
    )

    try {
        $entries = @(
            $archive.Entries |
                ForEach-Object {
                    $_.FullName.Replace("\", "/")
                }
        )

        $requiredEntries = @(
            "Agent/TitanMDM.WindowsAgent.exe",
            "RemoteHost/TitanMDM.RemoteHost.exe"
        )

        foreach ($required in $requiredEntries) {
            if ($entries -notcontains $required) {
                throw "El ZIP no contiene $required"
            }
        }
    }
    finally {
        $archive.Dispose()
    }

    Write-Host "[5/5] Generando manifiesto..." `
        -ForegroundColor Yellow

    $commit = ""
    $git = Get-Command git -ErrorAction SilentlyContinue

    if ($null -ne $git) {
        $commit = (
            & git -C $repoRoot rev-parse HEAD 2>$null
        ).Trim()
    }

    $manifest = [ordered]@{
        product = "TitanMDM Windows Agent"
        phase = "W11"
        builtAtUtc = [DateTime]::UtcNow.ToString("o")
        sourceCommit = $commit
        configuration = $Configuration
        runtime = $Runtime
        packageFile = $zipFile.Name
        packageBytes = $zipFile.Length
        packageSha256 = (
            Get-FileHash `
                -LiteralPath $zipPath `
                -Algorithm SHA256
        ).Hash
        agentSha256 = (
            Get-FileHash `
                -LiteralPath $agentExe `
                -Algorithm SHA256
        ).Hash
        remoteHostSha256 = (
            Get-FileHash `
                -LiteralPath $remoteHostExe `
                -Algorithm SHA256
        ).Hash
    }

    $manifest |
        ConvertTo-Json -Depth 4 |
        Set-Content `
            -LiteralPath $manifestPath `
            -Encoding UTF8

    Write-Host ""
    Write-Host "W11: compilación y paquete verificados." `
        -ForegroundColor Green

    Write-Host "ZIP: $zipPath"
    Write-Host "SHA256: $($manifest.packageSha256)"
    Write-Host "Manifiesto: $manifestPath"
}
finally {
    Pop-Location
}