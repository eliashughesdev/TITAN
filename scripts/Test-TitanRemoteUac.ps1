
#Requires -Version 5.1

<#
.SYNOPSIS
    TitanMDM Enterprise - Remote UAC Diagnostics

.DESCRIPTION
    Diagnostica servicio WindowsAgent, procesos RemoteHost,
    sesiones interactivas y politicas UAC.

    Solo lectura.
    No modifica configuraciones de seguridad.
    No inspecciona lineas de comandos ni tokens secretos.
#>

[CmdletBinding()]
param(
    [switch]$SaveReport
)

$ErrorActionPreference = "Stop"

$Root = "C:\TitanMDM"

$Report = [ordered]@{
    GeneratedAtUtc = [DateTime]::UtcNow.ToString("O")
    ComputerName = $env:COMPUTERNAME
    Windows = $null
    CurrentIdentity = $null
    WindowsAgentServices = @()
    RemoteHostProcesses = @()
    UacPolicies = [ordered]@{}
    InteractiveSessions = @()
    Findings = @()
}

function Write-Section {
    param([string]$Title)

    Write-Host ""
    Write-Host ("=" * 65) -ForegroundColor Cyan
    Write-Host $Title -ForegroundColor Cyan
    Write-Host ("=" * 65) -ForegroundColor Cyan
}

function Add-Finding {
    param(
        [string]$Level,
        [string]$Message
    )

    $script:Report.Findings += [pscustomobject]@{
        Level = $Level
        Message = $Message
    }

    $color = switch ($Level) {
        "OK" { "Green" }
        "WARNING" { "Yellow" }
        "ERROR" { "Red" }
        default { "Gray" }
    }

    Write-Host "[$Level] $Message" -ForegroundColor $color
}

function Read-UacValue {
    param(
        [string]$Name
    )

    $path = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"

    try {
        $properties = Get-ItemProperty -Path $path
        $value = $properties.PSObject.Properties[$Name]

        if ($null -eq $value) {
            return $null
        }

        return $value.Value
    }
    catch {
        return $null
    }
}

Write-Section "TITANMDM - RS-H3.3 UAC DIAGNOSTICS"

# ============================================================
# OPERATING SYSTEM
# ============================================================

Write-Section "1. WINDOWS"

$os = Get-CimInstance Win32_OperatingSystem

$Report.Windows = [pscustomobject]@{
    Caption = $os.Caption
    Version = $os.Version
    BuildNumber = $os.BuildNumber
    Architecture = $os.OSArchitecture
}

$Report.Windows | Format-List

# ============================================================
# CURRENT IDENTITY
# ============================================================

Write-Section "2. IDENTIDAD DEL PROCESO DE DIAGNOSTICO"

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()

$principal = [Security.Principal.WindowsPrincipal]::new(
    $identity
)

$isAdministrator = $principal.IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator
)

$Report.CurrentIdentity = [pscustomobject]@{
    IsAdministrator = $isAdministrator
    AuthenticationType = $identity.AuthenticationType
    IsSystem = $identity.IsSystem
    SessionId = [Diagnostics.Process]::GetCurrentProcess().SessionId
}

$Report.CurrentIdentity | Format-List

if ($isAdministrator) {
    Add-Finding "OK" "La consola de diagnostico tiene token administrativo habilitado."
}
else {
    Add-Finding "INFO" "El diagnostico se ejecuta sin elevacion administrativa."
}

# ============================================================
# WINDOWS AGENT
# ============================================================

Write-Section "3. WINDOWS AGENT SERVICE"

$services = @(
    Get-CimInstance Win32_Service |
        Where-Object {
            $_.Name -like "*TitanMDM*" -or
            $_.DisplayName -like "*TitanMDM*"
        }
)

foreach ($service in $services) {
    $entry = [pscustomobject]@{
        Name = $service.Name
        DisplayName = $service.DisplayName
        State = $service.State
        StartMode = $service.StartMode
        StartName = $service.StartName
        ProcessId = $service.ProcessId
    }

    $Report.WindowsAgentServices += $entry
    $entry | Format-List

    if (
        $service.State -eq "Running" -and
        $service.StartName -in @(
            "LocalSystem",
            "NT AUTHORITY\SYSTEM"
        )
    ) {
        Add-Finding "OK" "Servicio TitanMDM activo bajo LocalSystem."
    }
}

if ($services.Count -eq 0) {
    Add-Finding "WARNING" "No se encontro servicio TitanMDM instalado."
}

# ============================================================
# REMOTE HOST PROCESSES
# ============================================================

Write-Section "4. REMOTE HOST PROCESSES"

$remoteProcesses = @(
    Get-CimInstance Win32_Process |
        Where-Object {
            $_.Name -eq "TitanMDM.RemoteHost.exe"
        }
)

foreach ($process in $remoteProcesses) {
    $ownerName = "No disponible"

    try {
        $owner = Invoke-CimMethod `
            -InputObject $process `
            -MethodName GetOwner

        if ($owner.ReturnValue -eq 0) {
            $ownerName = "$($owner.Domain)\$($owner.User)"
        }
    }
    catch {
        # La consulta puede estar restringida.
    }

    $entry = [pscustomobject]@{
        ProcessId = $process.ProcessId
        ParentProcessId = $process.ParentProcessId
        SessionId = $process.SessionId
        Owner = $ownerName
        ExecutablePath = $process.ExecutablePath
    }

    $Report.RemoteHostProcesses += $entry
    $entry | Format-List
}

if ($remoteProcesses.Count -eq 0) {
    Add-Finding "WARNING" "RemoteHost no esta ejecutandose."
}
elseif ($remoteProcesses.Count -gt 1) {
    Add-Finding "WARNING" "Se detectaron multiples procesos RemoteHost."
}
else {
    Add-Finding "OK" "RemoteHost encontrado."
}

# ============================================================
# UAC POLICIES
# ============================================================

Write-Section "5. UAC ENTERPRISE POLICIES"

$policyNames = @(
    "EnableLUA",
    "PromptOnSecureDesktop",
    "ConsentPromptBehaviorAdmin",
    "ConsentPromptBehaviorUser",
    "FilterAdministratorToken",
    "EnableInstallerDetection",
    "ValidateAdminCodeSignatures"
)

foreach ($name in $policyNames) {
    $value = Read-UacValue $name
    $Report.UacPolicies[$name] = $value

    Write-Host "$name = $value"
}

if ($Report.UacPolicies["EnableLUA"] -eq 1) {
    Add-Finding "OK" "UAC esta habilitado."
}
elseif ($Report.UacPolicies["EnableLUA"] -eq 0) {
    Add-Finding "WARNING" "UAC aparece deshabilitado."
}
else {
    Add-Finding "INFO" "No se pudo determinar EnableLUA."
}

if ($Report.UacPolicies["PromptOnSecureDesktop"] -eq 1) {
    Add-Finding "OK" "Secure Desktop esta configurado para UAC."
}
elseif ($Report.UacPolicies["PromptOnSecureDesktop"] -eq 0) {
    Add-Finding "WARNING" "UAC no esta configurado para solicitar elevacion en Secure Desktop."
}
else {
    Add-Finding "INFO" "PromptOnSecureDesktop no pudo determinarse."
}

# ============================================================
# INTERACTIVE SESSIONS
# ============================================================

Write-Section "6. WINDOWS SESSIONS"

try {
    $sessionOutput = @(quser.exe 2>&1)

    foreach ($line in $sessionOutput) {
        Write-Host $line
        $Report.InteractiveSessions += [string]$line
    }
}
catch {
    Add-Finding "INFO" "No fue posible consultar las sesiones interactivas."
}

# ============================================================
# SUMMARY
# ============================================================

Write-Section "7. RESUMEN"

$Report.Findings |
    Format-Table -AutoSize

Write-Host ""
Write-Host "Diagnostico terminado sin modificar Windows." `
    -ForegroundColor Green

# ============================================================
# OPTIONAL REPORT
# ============================================================

if ($SaveReport) {
    $reportDirectory = Join-Path $Root "artifacts\diagnostics"

    New-Item `
        -ItemType Directory `
        -Path $reportDirectory `
        -Force | Out-Null

    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"

    $reportPath = Join-Path `
        $reportDirectory `
        "titanmdm-uac-$timestamp.json"

    $Report |
        ConvertTo-Json -Depth 8 |
        Set-Content `
            -Path $reportPath `
            -Encoding UTF8

    Write-Host ""
    Write-Host "Reporte generado: $reportPath" `
        -ForegroundColor Cyan
}
