param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference =
    "Stop"

$Root =
    Resolve-Path(
        Join-Path `
            $PSScriptRoot `
            "..\.."
    )

$AgentProject =
    Join-Path `
        $Root `
        "agents\windows\TitanMDM.WindowsAgent\TitanMDM.WindowsAgent.csproj"

$RemoteHostProject =
    Join-Path `
        $Root `
        "agents\windows\TitanMDM.RemoteHost\TitanMDM.RemoteHost.csproj"

$ArtifactsRoot =
    Join-Path `
        $Root `
        "artifacts\windows-agent"

$PackageRoot =
    Join-Path `
        $ArtifactsRoot `
        "package"

$AgentOutput =
    Join-Path `
        $PackageRoot `
        "Agent"

$RemoteHostOutput =
    Join-Path `
        $PackageRoot `
        "RemoteHost"

$ZipPath =
    Join-Path `
        $ArtifactsRoot `
        "TitanMDM-WindowsAgent-x64.zip"

Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host " TitanMDM Windows Distribution Builder" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

if (
    Test-Path $ArtifactsRoot
) {
    Remove-Item `
        $ArtifactsRoot `
        -Recurse `
        -Force
}

New-Item `
    -ItemType Directory `
    -Path $AgentOutput `
    -Force |
    Out-Null

New-Item `
    -ItemType Directory `
    -Path $RemoteHostOutput `
    -Force |
    Out-Null

Write-Host "[1/4] Publishing Windows Agent..." -ForegroundColor Yellow

dotnet publish `
    $AgentProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $AgentOutput

if (
    $LASTEXITCODE -ne 0
) {
    throw "Windows Agent publish failed."
}

Write-Host ""
Write-Host "[2/4] Publishing RemoteHost..." -ForegroundColor Yellow

dotnet publish `
    $RemoteHostProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $RemoteHostOutput

if (
    $LASTEXITCODE -ne 0
) {
    throw "RemoteHost publish failed."
}

$AgentExe =
    Join-Path `
        $AgentOutput `
        "TitanMDM.WindowsAgent.exe"

$RemoteHostExe =
    Join-Path `
        $RemoteHostOutput `
        "TitanMDM.RemoteHost.exe"

if (
    !(Test-Path $AgentExe)
) {
    throw "TitanMDM.WindowsAgent.exe was not generated."
}

if (
    !(Test-Path $RemoteHostExe)
) {
    throw "TitanMDM.RemoteHost.exe was not generated."
}

Write-Host ""
Write-Host "[3/4] Creating distribution ZIP..." -ForegroundColor Yellow

if (
    Test-Path $ZipPath
) {
    Remove-Item `
        $ZipPath `
        -Force
}

Compress-Archive `
    -Path `
        (Join-Path $PackageRoot "Agent"),
        (Join-Path $PackageRoot "RemoteHost") `
    -DestinationPath $ZipPath `
    -CompressionLevel Optimal `
    -Force

if (
    !(Test-Path $ZipPath)
) {
    throw "Distribution ZIP was not generated."
}

Write-Host ""
Write-Host "[4/4] Calculating SHA-256..." -ForegroundColor Yellow

$Hash =
    (
        Get-FileHash `
            -Path $ZipPath `
            -Algorithm SHA256
    ).Hash

$ZipSize =
    (
        Get-Item $ZipPath
    ).Length

Write-Host ""
Write-Host "=============================================" -ForegroundColor Green
Write-Host " TITANMDM PACKAGE READY" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green
Write-Host ""

Write-Host "ZIP:" -ForegroundColor Cyan
Write-Host $ZipPath

Write-Host ""
Write-Host "Size:"
Write-Host "$ZipSize bytes"

Write-Host ""
Write-Host "SHA256:"
Write-Host $Hash

Write-Host ""