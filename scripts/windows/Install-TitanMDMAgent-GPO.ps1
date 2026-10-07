param(
    [string]$ConfigPath = "",
    [switch]$ForceReinstall
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$ServiceName =
    "TitanMDMWindowsAgent"

$ProgramDataRoot =
    Join-Path `
        $env:ProgramData `
        "TitanMDM"

$LogDirectory =
    Join-Path `
        $ProgramDataRoot `
        "logs"

$LogPath =
    Join-Path `
        $LogDirectory `
        "gpo-bootstrap.log"

$LockDirectory =
    Join-Path `
        $ProgramDataRoot `
        "locks"

$LockPath =
    Join-Path `
        $LockDirectory `
        "gpo-install.lock"

$IdentityPath =
    Join-Path `
        $ProgramDataRoot `
        "device.json"

$SettingsPath =
    Join-Path `
        $ProgramDataRoot `
        "agentsettings.json"

function Write-GpoLog {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message,

        [ValidateSet(
            "INFO",
            "WARN",
            "ERROR"
        )]
        [string]$Level = "INFO"
    )

    try {
        if (
            !(Test-Path $LogDirectory)
        ) {
            New-Item `
                -ItemType Directory `
                -Path $LogDirectory `
                -Force |
            Out-Null
        }

        $timestamp =
            Get-Date `
                -Format "yyyy-MM-dd HH:mm:ss"

        Add-Content `
            -Path $LogPath `
            -Value "[$timestamp][$Level] $Message" `
            -Encoding UTF8
    }
    catch {
    }
}

function Test-IsSystem {
    $identity =
        [Security.Principal
            .WindowsIdentity]
            ::GetCurrent()

    return (
        $identity.Name -eq
        "NT AUTHORITY\SYSTEM"
    )
}

function Acquire-InstallLock {
    if (
        !(Test-Path $LockDirectory)
    ) {
        New-Item `
            -ItemType Directory `
            -Path $LockDirectory `
            -Force |
        Out-Null
    }

    if (
        Test-Path $LockPath
    ) {
        try {
            $existing =
                Get-Content `
                    $LockPath `
                    -Raw |
                ConvertFrom-Json

            $age =
                (
                    Get-Date
                ) -
                [DateTime]$existing.createdAt

            if (
                $age.TotalMinutes -lt
                30
            ) {
                Write-GpoLog `
                    "Existe otra instalación TitanMDM en progreso." `
                    "WARN"

                exit 0
            }
        }
        catch {
        }

        Remove-Item `
            $LockPath `
            -Force `
            -ErrorAction SilentlyContinue
    }

    @{
        computer =
            $env:COMPUTERNAME

        processId =
            $PID

        createdAt =
            (
                Get-Date
            ).ToString("o")
    } |
    ConvertTo-Json |
    Set-Content `
        $LockPath `
        -Encoding UTF8
}

function Release-InstallLock {
    Remove-Item `
        $LockPath `
        -Force `
        -ErrorAction SilentlyContinue
}

function Test-HealthyInstallation {
    $service =
        Get-Service `
            -Name $ServiceName `
            -ErrorAction SilentlyContinue

    $agentExe =
        Join-Path `
            $env:ProgramFiles `
            "TitanMDM\Agent\TitanMDM.WindowsAgent.exe"

    if (
        $null -eq $service
    ) {
        return $false
    }

    if (
        !(Test-Path $agentExe)
    ) {
        return $false
    }

    if (
        !(Test-Path $SettingsPath)
    ) {
        return $false
    }

    if (
        !(Test-Path $IdentityPath)
    ) {
        return $false
    }

    if (
        $service.Status -ne
        "Running"
    ) {
        try {
            Start-Service `
                -Name $ServiceName `
                -ErrorAction Stop

            $service.WaitForStatus(
                "Running",
                [TimeSpan]::FromSeconds(20)
            )
        }
        catch {
            return $false
        }
    }

    return $true
}

try {
    Write-GpoLog `
        "============================================="

    Write-GpoLog `
        "TitanMDM Enterprise GPO Bootstrap"

    Write-GpoLog `
        "Equipo=$env:COMPUTERNAME"

    Write-GpoLog `
        "Usuario=$([Security.Principal.WindowsIdentity]::GetCurrent().Name)"

    if (
        !(Test-IsSystem)
    ) {
        Write-GpoLog `
            "El bootstrap GPO no se ejecuta como SYSTEM." `
            "WARN"
    }

    Acquire-InstallLock

    if (
        [string]::IsNullOrWhiteSpace(
            $ConfigPath)
    ) {
        $ConfigPath =
            Join-Path `
                $PSScriptRoot `
                "config.json"
    }

    if (
        !(Test-Path $ConfigPath)
    ) {
        throw `
            "No existe config.json: $ConfigPath"
    }

    $mainInstaller =
        Join-Path `
            $PSScriptRoot `
            "Install-TitanMDMAgent.ps1"

    if (
        !(Test-Path $mainInstaller)
    ) {
        throw `
            "No existe Install-TitanMDMAgent.ps1."
    }

    if (
        (Test-HealthyInstallation)
        -and
        !$ForceReinstall
    ) {
        Write-GpoLog `
            "TitanMDM ya está instalado, enrolado y Running."

        Release-InstallLock

        exit 0
    }

    Write-GpoLog `
        "La instalación requiere reparación o instalación."

    $arguments =
        @(
            "-NoLogo",
            "-NoProfile",
            "-NonInteractive",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            "`"$mainInstaller`"",
            "-ConfigPath",
            "`"$ConfigPath`"",
            "-Silent",
            "-ForceReinstall"
        )

    $process =
        Start-Process `
            -FilePath `
                "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" `
            -ArgumentList `
                $arguments `
            -WindowStyle Hidden `
            -Wait `
            -PassThru

    if (
        $process.ExitCode -ne
        0
    ) {
        throw `
            "Instalador TitanMDM terminó con código $($process.ExitCode)."
    }

    Start-Sleep `
        -Seconds 5

    if (
        !(Test-HealthyInstallation)
    ) {
        throw `
            "TitanMDM terminó la instalación pero no superó la validación final."
    }

    Write-GpoLog `
        "TitanMDM quedó instalado, enrolado y operativo."

    Release-InstallLock

    exit 0
}
catch {
    Write-GpoLog `
        $_.Exception.Message `
        "ERROR"

    Release-InstallLock

    exit 1
}