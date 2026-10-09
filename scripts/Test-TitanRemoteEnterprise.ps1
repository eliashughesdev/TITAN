
#Requires -Version 5.1

<#
.SYNOPSIS
  TitanMDM - Enterprise Remote Support Validation

.DESCRIPTION
  Diagnostico no invasivo del equipo Windows.
  Comprueba servicio, RemoteHost, sesion interactiva,
  politicas UAC y preparacion para UIAccess.

  No cambia GPO, UAC, Defender ni el registro.
  No recopila contrasenas o argumentos de procesos.
#>

[CmdletBinding()]
param(
    [string]$OutputDirectory = "$env:TEMP\TitanMDM-Diagnostics"
)

$ErrorActionPreference = "Stop"

New-Item -ItemType Directory `
    -Path $OutputDirectory `
    -Force | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"

$reportPath = Join-Path `
    $OutputDirectory `
    "TitanRemote-$timestamp.json"

$report = [ordered]@{
    GeneratedAtUtc = [DateTime]::UtcNow.ToString("O")
    ComputerName = $env:COMPUTERNAME
    OperatingSystem = $null
    Services = @()
    RemoteHost = @()
    Policies = [ordered]@{}
    Findings = @()
}

function Add-Finding {
    param(
        [string]$Severity,
        [string]$Description
    )

    $script:report.Findings += [pscustomobject]@{
        Severity = $Severity
        Description = $Description
    }

    Write-Host "[$Severity] $Description"
}

function Get-PolicyValue {
    param([string]$Name)

    $path = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"

    try {
        return Get-ItemPropertyValue `
            -Path $path `
            -Name $Name `
            -ErrorAction Stop
    }
    catch {
        return $null
    }
}

Write-Host ""
Write-Host "TITANMDM - ENTERPRISE REMOTE DIAGNOSTICS"
Write-Host "======================================" 

$os = Get-CimInstance Win32_OperatingSystem

$report.OperatingSystem = [pscustomobject]@{
    Name = $os.Caption
    Version = $os.Version
    Build = $os.BuildNumber
}

# ============================================================
# WINDOWS SERVICES
# ============================================================

$services = @(
    Get-CimInstance Win32_Service |
        Where-Object {
            $_.Name -like "*TitanMDM*" -or
            $_.DisplayName -like "*TitanMDM*"
        }
)

foreach ($service in $services) {
    $report.Services += [pscustomobject]@{
        Name = $service.Name
        State = $service.State
        StartMode = $service.StartMode
        Account = $service.StartName
        ProcessId = $service.ProcessId
    }
}

if ($services.Count -eq 0) {
    Add-Finding "INFO" "No hay un servicio TitanMDM registrado."
}
else {
    foreach ($service in $services) {
        Add-Finding "INFO" (
            "$($service.Name): $($service.State), " +
            "cuenta=$($service.StartName)"
        )
    }
}

# ============================================================
# REMOTE HOST
# ============================================================

$hosts = @(
    Get-CimInstance Win32_Process |
        Where-Object {
            $_.Name -eq "TitanMDM.RemoteHost.exe"
        }
)

foreach ($hostProcess in $hosts) {
    $report.RemoteHost += [pscustomobject]@{
        ProcessId = $hostProcess.ProcessId
        SessionId = $hostProcess.SessionId
        ParentProcessId = $hostProcess.ParentProcessId
    }
}

Add-Finding "INFO" (
    "Procesos RemoteHost encontrados: $($hosts.Count)"
)

# ============================================================
# UAC POLICIES
# ============================================================

$policies = @(
    "EnableLUA",
    "PromptOnSecureDesktop",
    "ConsentPromptBehaviorAdmin",
    "ConsentPromptBehaviorUser",
    "EnableUIADesktopToggle",
    "EnableSecureUIAPaths",
    "ValidateAdminCodeSignatures"
)

foreach ($policy in $policies) {
    $value = Get-PolicyValue $policy

    $report.Policies[$policy] = $value

    $display = if ($null -eq $value) {
        "NO DEFINIDO"
    }
    else {
        [string]$value
    }

    Write-Host "$policy = $display"
}

# ============================================================
# SECURITY FINDINGS
# ============================================================

if ($report.Policies["EnableLUA"] -eq 1) {
    Add-Finding "OK" "UAC habilitado."
}
else {
    Add-Finding "REVIEW" "Verificar configuracion UAC."
}

if ($report.Policies["PromptOnSecureDesktop"] -eq 1) {
    Add-Finding "OK" "Secure Desktop habilitado."

    Add-Finding "INFO" (
        "La captura GDI normal no garantiza visualizar UAC."
    )
}
elseif ($report.Policies["PromptOnSecureDesktop"] -eq 0) {
    Add-Finding "REVIEW" (
        "Secure Desktop esta configurado como deshabilitado."
    )
}
else {
    Add-Finding "INFO" (
        "Verificar politica efectiva de Secure Desktop."
    )
}

if ($report.Policies["EnableUIADesktopToggle"] -eq 0) {
    Add-Finding "BLOCKED" (
        "El cambio de escritorio mediante UIAccess " +
        "esta deshabilitado por politica."
    )
}

if ($report.Policies["ConsentPromptBehaviorUser"] -eq 3) {
    Add-Finding "INFO" (
        "Los usuarios estandar requieren credenciales " +
        "para elevar privilegios."
    )
}

# ============================================================
# SAVE REPORT
# ============================================================

$report |
    ConvertTo-Json -Depth 8 |
    Set-Content `
        -Path $reportPath `
        -Encoding UTF8

Write-Host ""
Write-Host "======================================"
Write-Host "DIAGNOSTICO FINALIZADO"
Write-Host "Archivo: $reportPath"
Write-Host ""
Write-Host "No se modificaron politicas de seguridad."
