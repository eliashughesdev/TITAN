param(
    [switch]$PurgeIdentity
)

$ErrorActionPreference =
    "Stop"

$ServiceName =
    "TitanMDMWindowsAgent"

$InstallRoot =
    Join-Path `
        $env:ProgramFiles `
        "TitanMDM"

$ProgramDataRoot =
    Join-Path `
        $env:ProgramData `
        "TitanMDM"

function Assert-Administrator {
    $identity =
        [Security.Principal.WindowsIdentity]::GetCurrent()

    $principal =
        New-Object `
            Security.Principal.WindowsPrincipal(
                $identity
            )

    if (
        !$principal.IsInRole(
            [Security.Principal.WindowsBuiltInRole]::Administrator
        )
    ) {
        throw "Ejecuta PowerShell como Administrador."
    }
}

Assert-Administrator

Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host " TitanMDM Windows Agent Uninstaller" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

$service =
    Get-Service `
        -Name $ServiceName `
        -ErrorAction SilentlyContinue

if ($service) {
    Write-Host "Deteniendo servicio..."

    if (
        $service.Status -ne
        "Stopped"
    ) {
        Stop-Service `
            -Name $ServiceName `
            -Force

        $service.WaitForStatus(
            "Stopped",
            [TimeSpan]::FromSeconds(20)
        )
    }

    Write-Host "Eliminando servicio..."

    sc.exe delete `
        $ServiceName |
        Out-Null

    Start-Sleep `
        -Seconds 2
}

if (
    Test-Path $InstallRoot
) {
    Write-Host "Eliminando binarios..."

    Remove-Item `
        $InstallRoot `
        -Recurse `
        -Force
}

if ($PurgeIdentity) {
    if (
        Test-Path $ProgramDataRoot
    ) {
        Write-Host "Eliminando identidad y configuración..."

        Remove-Item `
            $ProgramDataRoot `
            -Recurse `
            -Force
    }
}
else {
    Write-Host ""
    Write-Host "La identidad TitanMDM fue preservada." -ForegroundColor Yellow
    Write-Host "Para eliminarla utiliza:"
    Write-Host ".\Uninstall-TitanMDMAgent.ps1 -PurgeIdentity"
}

Write-Host ""
Write-Host "TitanMDM Windows Agent desinstalado." -ForegroundColor Green