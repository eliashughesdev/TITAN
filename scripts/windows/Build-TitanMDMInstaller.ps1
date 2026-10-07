param(
    [Parameter(Mandatory = $true)]
    [string]$ConfigPath,

    [string]$OutputDirectory = "",

    [string]$OutputName = "TitanMDM-Agent-Setup"
)

$ErrorActionPreference =
    "Stop"

$Root =
    Resolve-Path(
        Join-Path `
            $PSScriptRoot `
            "..\.."
    )

if (
    [string]::IsNullOrWhiteSpace(
        $OutputDirectory
    )
) {
    $OutputDirectory =
        Join-Path `
            $Root `
            "artifacts\windows-installer"
}

$InstallerScript =
    Join-Path `
        $PSScriptRoot `
        "Install-TitanMDMAgent.ps1"

$IssPath =
    Join-Path `
        $PSScriptRoot `
        "Setup-TitanMDM.iss"

$InnoCompilerCandidates =
    @(
        "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )

$InnoCompiler =
    $InnoCompilerCandidates |
    Where-Object {
        Test-Path $_
    } |
    Select-Object `
        -First 1

if (
    !$InnoCompiler
) {
    throw "No se encontró Inno Setup 6 (ISCC.exe)."
}

if (
    !(Test-Path $ConfigPath)
) {
    throw "No existe config.json: $ConfigPath"
}

if (
    !(Test-Path $InstallerScript)
) {
    throw "No existe Install-TitanMDMAgent.ps1."
}

if (
    !(Test-Path $IssPath)
) {
    throw "No existe Setup-TitanMDM.iss."
}

$BuildRoot =
    Join-Path `
        $env:TEMP `
        (
            "TitanMDM-Installer-" +
            [Guid]::NewGuid().ToString("N")
        )

New-Item `
    -ItemType Directory `
    -Path $BuildRoot `
    -Force |
    Out-Null

New-Item `
    -ItemType Directory `
    -Path $OutputDirectory `
    -Force |
    Out-Null

try {
    Copy-Item `
        $InstallerScript `
        (Join-Path $BuildRoot "Install-TitanMDMAgent.ps1") `
        -Force

    Copy-Item `
        $ConfigPath `
        (Join-Path $BuildRoot "config.json") `
        -Force

    Write-Host ""
    Write-Host "Compilando TitanMDM Setup..." -ForegroundColor Cyan
    Write-Host ""

    & $InnoCompiler `
        "/DSourceRoot=$BuildRoot" `
        "/DOutputDir=$OutputDirectory" `
        "/DOutputName=$OutputName" `
        $IssPath

    if (
        $LASTEXITCODE -ne 0
    ) {
        throw "Inno Setup devolvió código $LASTEXITCODE"
    }

    $SetupPath =
        Join-Path `
            $OutputDirectory `
            "$OutputName.exe"

    if (
        !(Test-Path $SetupPath)
    ) {
        throw "No se generó $SetupPath"
    }

    Write-Host ""
    Write-Host "Instalador generado:" -ForegroundColor Green
    Write-Host $SetupPath

    Get-FileHash `
        $SetupPath `
        -Algorithm SHA256
}
finally {
    if (
        Test-Path $BuildRoot
    ) {
        Remove-Item `
            $BuildRoot `
            -Recurse `
            -Force
    }
}