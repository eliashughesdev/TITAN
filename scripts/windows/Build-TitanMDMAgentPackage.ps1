
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [ValidateSet("win-x64")]
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$Root = [System.IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot "..\..")
)

$AgentProject = Join-Path $Root `
    "agents\windows\TitanMDM.WindowsAgent\TitanMDM.WindowsAgent.csproj"

$HostProject = Join-Path $Root `
    "agents\windows\TitanMDM.RemoteHost\TitanMDM.RemoteHost.csproj"

$NativeSource = Join-Path $Root `
    "agents\windows\TitanMDM.RemoteHost.Native\TitanDxgiCapture.cpp"

$ArtifactsRoot = Join-Path $Root "artifacts\windows-agent"
$BackupsRoot = Join-Path $Root "artifacts\windows-agent-backups"
$PackageRoot = Join-Path $ArtifactsRoot "package"
$AgentOutput = Join-Path $PackageRoot "Agent"
$HostOutput = Join-Path $PackageRoot "RemoteHost"
$NativeBuild = Join-Path $ArtifactsRoot "native-build"

$ZipPath = Join-Path $ArtifactsRoot `
    "TitanMDM-WindowsAgent-x64.zip"

$ManifestPath = Join-Path $ArtifactsRoot `
    "TitanMDM-WindowsAgent-x64.manifest.json"

function Assert-ExitCode {
    param([string]$Operation)

    if ($LASTEXITCODE -ne 0) {
        throw "$Operation falló. Código: $LASTEXITCODE"
    }
}

function Get-FileRecord {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Archivo no encontrado: $Path"
    }

    $item = Get-Item -LiteralPath $Path

    return [ordered]@{
        Name = $item.Name
        Bytes = $item.Length
        Sha256 = (Get-FileHash `
            -LiteralPath $Path `
            -Algorithm SHA256).Hash
    }
}

Write-Host "`n=== TITANMDM WINDOWS RELEASE BUILDER ===" `
    -ForegroundColor Cyan

if (!(Test-Path $AgentProject)) {
    throw "Falta WindowsAgent.csproj"
}

if (!(Test-Path $HostProject)) {
    throw "Falta RemoteHost.csproj"
}

if (!(Test-Path $NativeSource)) {
    throw "Falta TitanDxgiCapture.cpp"
}

$branch = git -C $Root branch --show-current
Assert-ExitCode "Consulta de rama Git"

if ($branch -ne "feat/remote-support-rs-h") {
    throw "Rama incorrecta: $branch"
}

Write-Host "`n[1/7] Respaldando paquete anterior..." `
    -ForegroundColor Yellow

New-Item -ItemType Directory -Path $BackupsRoot -Force |
    Out-Null

if (Test-Path $ZipPath) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"

    Copy-Item -LiteralPath $ZipPath `
        -Destination (Join-Path $BackupsRoot `
            "TitanMDM-WindowsAgent-$stamp.zip")
}

Write-Host "`n[2/7] Preparando herramientas MSVC..." `
    -ForegroundColor Yellow

$vswhere = Join-Path ${env:ProgramFiles(x86)} `
    "Microsoft Visual Studio\Installer\vswhere.exe"

if (!(Test-Path $vswhere)) {
    throw "No existe vswhere.exe. Instala Visual Studio C++ Build Tools."
}

$vsPath = & $vswhere `
    -latest `
    -products "*" `
    -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 `
    -property installationPath

Assert-ExitCode "Búsqueda MSVC"

if (-not $vsPath) {
    throw "No se encontró MSVC para x64."
}

$vcvars = Join-Path $vsPath `
    "VC\Auxiliary\Build\vcvars64.bat"

if (!(Test-Path $vcvars)) {
    throw "Falta vcvars64.bat"
}

if (Test-Path $ArtifactsRoot) {
    Remove-Item -LiteralPath $ArtifactsRoot -Recurse -Force
}

foreach ($dir in @(
    $AgentOutput,
    $HostOutput,
    $NativeBuild
)) {
    New-Item -ItemType Directory -Path $dir -Force |
        Out-Null
}

$nativeDll = Join-Path $NativeBuild "TitanDxgiCapture.dll"

# Generar un comando temporal aislado para MSVC.
# No altera PATH global ni modifica configuraciones de Windows.
$buildCmd = Join-Path $NativeBuild "build-native.cmd"

$cmdContent = @"
@echo off
call "$vcvars"
if errorlevel 1 exit /b 1
cl /nologo /std:c++17 /EHsc /O2 /LD "$NativeSource" /link /OUT:"$nativeDll" d3d11.lib dxgi.lib user32.lib
exit /b %errorlevel%
"@

[System.IO.File]::WriteAllText(
    $buildCmd,
    $cmdContent,
    [System.Text.Encoding]::ASCII
)

Write-Host "`n[3/7] Compilando DXGI x64..." `
    -ForegroundColor Yellow

Push-Location $NativeBuild

try {
    & cmd.exe /d /c "`"$buildCmd`""
    Assert-ExitCode "Compilación DXGI"
}
finally {
    Pop-Location
}

if (!(Test-Path $nativeDll)) {
    throw "No se generó TitanDxgiCapture.dll"
}

Write-Host "`n[4/7] Publicando WindowsAgent..." `
    -ForegroundColor Yellow

dotnet publish $AgentProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $AgentOutput

Assert-ExitCode "Publicación WindowsAgent"

Write-Host "`n[5/7] Publicando RemoteHost..." `
    -ForegroundColor Yellow

dotnet publish $HostProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $HostOutput

Assert-ExitCode "Publicación RemoteHost"

# DXGI siempre se distribuye junto al ejecutable.
$installedDll = Join-Path $HostOutput "TitanDxgiCapture.dll"

Copy-Item -LiteralPath $nativeDll `
    -Destination $installedDll -Force

$agentExe = Join-Path $AgentOutput `
    "TitanMDM.WindowsAgent.exe"

$hostExe = Join-Path $HostOutput `
    "TitanMDM.RemoteHost.exe"

$files = @(
    $agentExe,
    $hostExe,
    $installedDll
)

foreach ($file in $files) {
    if (!(Test-Path $file)) {
        throw "Falta componente de distribución: $file"
    }
}

Write-Host "`n[6/7] Empaquetando y verificando ZIP..." `
    -ForegroundColor Yellow

Compress-Archive `
    -Path (Join-Path $PackageRoot "Agent"), `
          (Join-Path $PackageRoot "RemoteHost") `
    -DestinationPath $ZipPath `
    -CompressionLevel Optimal `
    -Force

if (!(Test-Path $ZipPath)) {
    throw "No se generó el ZIP"
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

$archive = [System.IO.Compression.ZipFile]::OpenRead(
    $ZipPath
)

try {
    $entries = @(
        $archive.Entries | ForEach-Object {
            $_.FullName.Replace('\', '/')
        }
    )

    foreach ($required in @(
        "Agent/TitanMDM.WindowsAgent.exe",
        "RemoteHost/TitanMDM.RemoteHost.exe",
        "RemoteHost/TitanDxgiCapture.dll"
    )) {
        if ($required -notin $entries) {
            throw "ZIP incompleto: $required"
        }

        Write-Host "OK: $required" -ForegroundColor Green
    }
}
finally {
    $archive.Dispose()
}

Write-Host "`n[7/7] Generando manifiesto..." `
    -ForegroundColor Yellow

$manifest = [ordered]@{
    Product = "TitanMDM Windows Agent"
    Configuration = $Configuration
    Runtime = $Runtime
    Branch = $branch
    Commit = (git -C $Root rev-parse HEAD)
    BuiltAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    Components = @(
        (Get-FileRecord $agentExe),
        (Get-FileRecord $hostExe),
        (Get-FileRecord $installedDll)
    )
    Package = Get-FileRecord $ZipPath
}

$manifest |
    ConvertTo-Json -Depth 8 |
    Set-Content -LiteralPath $ManifestPath -Encoding UTF8

Write-Host "`n=== PAQUETE GENERADO ===" `
    -ForegroundColor Green

Write-Host "ZIP: $ZipPath"
Write-Host "Manifiesto: $ManifestPath"

Write-Host "SHA256:" `
    -ForegroundColor Cyan

(Get-FileHash $ZipPath -Algorithm SHA256).Hash

Write-Host "`nTitanMDM WindowsAgent + RemoteHost + DXGI listos." `
    -ForegroundColor Green
